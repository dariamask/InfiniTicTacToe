using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Models;

public class UserInfo(string id, string nickName, DateTimeOffset connectedAt, WebSocket webSocket)
{
    public string Id { get; set; } = id;

    public string Nickname { get; set; } = nickName;

    public DateTimeOffset ConnectedAt { get; set; } = connectedAt;

    public WebSocket WebSocket { get; set; } = webSocket;
}
