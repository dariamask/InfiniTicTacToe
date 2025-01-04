using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Services
{
    public interface IWebSocketGameManager
    {
        event EventHandler<WebsocketConnectionEventArgs>? ConnectionReceived;
        event EventHandler<WebsocketMessageEventArgs>? MessageReceived;

        Task ReceiveMessagesAsync(string id, WebSocket socket, TaskCompletionSource<object> socketFinishedTcs, CancellationToken cancellationToken);
        Task SendMessageAsync(string id, object message);
    }
}