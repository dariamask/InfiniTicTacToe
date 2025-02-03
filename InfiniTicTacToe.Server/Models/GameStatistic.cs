namespace InfiniTicTacToe.Server.Models;

public sealed record GameStatistics(
    int Games,
    int Players,
    IReadOnlyCollection<GameScore> GameScores);
