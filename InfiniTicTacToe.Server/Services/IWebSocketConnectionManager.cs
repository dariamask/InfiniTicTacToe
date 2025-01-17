using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Services
{
    public interface IWebSocketConnectionManager
    {
        event EventHandler<WebsocketConnectionEventArgs>? ConnectionReceived;

        event EventHandler<WebsocketConnectionEventArgs>? ConnectionClosed;

        event EventHandler<WebsocketMessageEventArgs>? MessageReceived;

        Task ReceiveMessagesAsync(string id, WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, CancellationToken cancellationToken);

        Task SendMessageAsync(string id, object message);

        void AddSocket(string socketId, WebSocket socket);

        WebSocket? GetSocketById(string socketId);

        Task RemoveSocket(string? socketId, CancellationToken cancellationToken);
    }
}
