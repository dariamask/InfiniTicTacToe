using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace InfiniTicTacToe.Server.Services;

public sealed record WebsocketMessageEventArgs(string Message, string SocketId);

public sealed record WebsocketConnectionEventArgs(string SocketId);

public class WebSocketGameManager : IWebSocketGameManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public event EventHandler<WebsocketMessageEventArgs>? MessageReceived;

    public event EventHandler<WebsocketConnectionEventArgs>? ConnectionReceived;

    public void AddSocket(string id, WebSocket socket)
    {
        _sockets.TryAdd(id, socket);
        ConnectionReceived?.Invoke(this, new WebsocketConnectionEventArgs(id));
    }

    public async Task RemoveSocket(string id)
    {
        if (_sockets.TryRemove(id, out var socket))
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", CancellationToken.None);
        }
    }

    public async Task SendMessageAsync(string id, object message)
    {
        if (_sockets.TryGetValue(id, out var socket) && socket.State == WebSocketState.Open)
        {
            var jsonMessage = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(jsonMessage);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public async Task ReceiveMessagesAsync(string id, WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await RemoveSocket(id);
            }
            else
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                OnMessageReceived(message, id);
            }
        }
    }

    protected virtual void OnMessageReceived(string message, string socketId)
    {
        MessageReceived?.Invoke(this, new(message, socketId));
    }
}