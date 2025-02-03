using System.Collections.Concurrent;

namespace InfiniTicTacToe.Server.Models;

public sealed record Game(string Id)
{
    private int _statusInt = (int)GameStatus.Pending;

    public string Id { get; } = Id;

    public Player? PlayerX { get; set; }

    public Player? PlayerO { get; set; }

    public GameStatus Status => (GameStatus)_statusInt;

    public ConcurrentDictionary<(int X, int Y), Cell> Board { get; } = new();

    public string? CurrentPlayerId { get; set; }

    public int ScoreX { get; set; }

    public int ScoreO { get; set; }

    public bool TryStartGame()
    {
        return Interlocked.CompareExchange(
            location1: ref _statusInt,
            value: (int)GameStatus.InProgress,
            comparand: (int)GameStatus.Pending) == (int)GameStatus.Pending;
    }

    public bool TryFinishGame()
    {
        return Interlocked.CompareExchange(
            location1: ref _statusInt,
            value: (int)GameStatus.Finished,
            comparand: (int)GameStatus.InProgress) == (int)GameStatus.InProgress;
    }
}
