using System.Collections.Concurrent;

using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed class GameStorage
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, Player> _players = new();

    public ConcurrentDictionary<string, Game> Games => _games;

    public ConcurrentDictionary<string, Player> Players => _players;

    public GameStatistics GetStats()
    {
        return new GameStatistics(
            Games.Count,
            Players.Count,
            Games.Values.Select(g => new GameScore(g.Id, g.ScoreX, g.ScoreO)).ToList());
    }
}
