using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Services
{
    public interface IWebSocketGameManager
    {
        event EventHandler<WebsocketConnectionEventArgs>? ConnectionReceived;
        event EventHandler<WebsocketMessageEventArgs>? MessageReceived;

        void AddSocket(string id, WebSocket socket);
        Task ReceiveMessagesAsync(string id, WebSocket socket, CancellationToken cancellationToken);
        Task RemoveSocket(string id);
        Task SendMessageAsync(string id, object message);
    }
}