namespace InfiniTicTacToe.Server.Models;

public sealed record GameScore(
    string GameId,
    int X,
    int O);
