namespace InfiniTicTacToe.Server.Models;

public sealed record Player(string Id, string Nickname)
{
    public Game? CurrentGame { get; set; }

    public Side? Side { get; set; }
}
