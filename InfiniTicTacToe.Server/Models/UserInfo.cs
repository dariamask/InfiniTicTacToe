using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Models;

public class UserInfo(string id, DateTimeOffset connectedAt, WebSocket webSocket)
{
    public string Id { get; set; } = id;

    public DateTimeOffset ConnectedAt { get; set; } = connectedAt;

    public WebSocket WebSocket { get; set; } = webSocket;
}
