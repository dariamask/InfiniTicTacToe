using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed record WebsocketMessageEventArgs(string Message, string SocketId);

public sealed record WebsocketConnectionEventArgs(string SocketId);

public class WebSocketConnectionManager(ILogger<WebSocketConnectionManager> logger)
    : IWebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, UserInfo> _userInfo = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public event EventHandler<WebsocketMessageEventArgs>? MessageReceived;

    public event EventHandler<WebsocketConnectionEventArgs>? ConnectionReceived;

    public event EventHandler<WebsocketConnectionEventArgs>? ConnectionClosed;

    public async Task SendMessageAsync(string id, object message)
    {
        if (_userInfo.TryGetValue(id, out var userInfo) && userInfo.WebSocket.State == WebSocketState.Open)
        {
            var jsonMessage = JsonSerializer.Serialize(message, _jsonSerializerOptions);
            var buffer = Encoding.UTF8.GetBytes(jsonMessage);
            await userInfo.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public async Task ReceiveMessagesAsync(string id, WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, CancellationToken cancellationToken)
    {
        AddSocket(id, socket);

        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await RemoveSocket(id);
                    socketFinishedTcs.SetResult(result);
                    return;
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    OnMessageReceived(message, id);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error receiving message");
                socketFinishedTcs.SetException(ex);
            }
        }

        await RemoveSocket(id);
    }

    protected virtual void OnMessageReceived(string message, string socketId)
    {
        MessageReceived?.Invoke(this, new(message, socketId));
    }

    private void AddSocket(string id, WebSocket socket)
    {
        var userInfo = new UserInfo(id, "Unknown", DateTimeOffset.UtcNow, socket);
        _userInfo.TryAdd(id, userInfo);
        //_ = SendMessageAsync(id, new { type = "hello" });
        ConnectionReceived?.Invoke(this, new WebsocketConnectionEventArgs(id));
    }

    private async Task RemoveSocket(string id)
    {
        if (_userInfo.TryRemove(id, out var userInfo))
        {
            await userInfo.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", CancellationToken.None);
            ConnectionClosed?.Invoke(this, new WebsocketConnectionEventArgs(id));
        }
    }
}