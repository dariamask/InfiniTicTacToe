using System.Collections.Concurrent;

using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed class GameStorage
{
    public ConcurrentDictionary<string, Game> Games { get; } = new();

    public ConcurrentDictionary<string, Player> Players { get; } = new();

    public GameStatistics GetStats()
    {
        return new GameStatistics(
            Games.Count,
            Players.Count,
            Games.Values.Select(g => new GameScore(g.Id, g.ScoreX, g.ScoreO)).ToList());
    }
}
