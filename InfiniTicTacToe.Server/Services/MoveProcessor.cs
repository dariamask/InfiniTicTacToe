using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed record MoveResult(bool IsAccepted, bool IsWin, IReadOnlyCollection<Cell> CrossedOutCells, string Message)
{
    public static MoveResult Success(bool isWin, IReadOnlyCollection<Cell> crossedOutCells, string message) =>
        new(true, isWin, crossedOutCells, message);

    public static MoveResult Fail(string message) => new(false, false, [], message);
}

public sealed class MoveProcessor(ILogger<MoveProcessor> logger)
{
    internal const int MaxX = 100;
    internal const int MaxY = 100;

    public bool IsPlayerTurn(Game game, string id) => game.CurrentPlayerId == id;

    public MoveResult MakeMove(Game game, Player player, int x, int y)
    {
        if (game.Status != GameStatus.InProgress)
            return MoveResult.Fail("Game is not in progress.");

        if (!IsPlayerTurn(game, player.Id))
            return MoveResult.Fail("It's not your turn.");

        if (!IsValidPosition((x, y)))
            return MoveResult.Fail("Move out of bounds.");

        var playerSide = player.Side ?? throw new InvalidOperationException("Player side is not set.");

        var move = new Cell(x, y, playerSide);
        var moveSuccess = game.Board.TryAdd((x, y), move);

        if (!moveSuccess)
            return MoveResult.Fail("Position already used.");

        var (isWin, crossedOutCells) = CheckWin(game, move, playerSide);
        if (isWin)
        {
            switch (player.Side)
            {
                case Side.X:
                    game.ScoreX++;
                    break;
                case Side.O:
                    game.ScoreO++;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown side: {player.Side}");
            }
        }

        logger.LogInformation("Move accepted: isWin = {IsWin}, number of crossed cells = {CrossedOutCells}", isWin, crossedOutCells.Count);
        return MoveResult.Success(isWin, crossedOutCells, "Move accepted.");
    }

    private static bool CheckDirection(Game game, Cell move, Side side, int dx, int dy, out List<Cell> cellsToBeCrossedOut)
    {
        var x = move.X;
        var y = move.Y;

        var count = 1;
        cellsToBeCrossedOut = [];

        for (var i = 1; i < 5; i++)
        {
            var position = (x + (i * dx), y + (i * dy));
            if (IsValidPosition(position)
                && game.Board.TryGetValue(position, out var cell)
                && cell.Side == side
                && !cell.CrossedOut)
            {
                count++;
                cellsToBeCrossedOut.Add(cell);
            }
            else
            {
                break;
            }
        }

        for (var i = 1; i < 5; i++)
        {
            var position = (x - (i * dx), y - (i * dy));
            if (IsValidPosition(position)
                && game.Board.TryGetValue(position, out var cell)
                && cell.Side == side
                && !cell.CrossedOut)
            {
                count++;
                cellsToBeCrossedOut.Add(cell);
            }
            else
            {
                break;
            }
        }

        // Cross out the winning cells, greedily if 6 or more cells in a row
        if (count < 5)
        {
            cellsToBeCrossedOut.Clear();
            return false;
        }

        cellsToBeCrossedOut.Add(move);
        foreach (var (crossOutX, crossOutY, _) in cellsToBeCrossedOut)
            game.Board[(crossOutX, crossOutY)].CrossedOut = true;

        return true;
    }

    private static bool IsValidPosition((int X, int Y) p) => p.X >= 0 && p.Y >= 0 && p.X < MaxX && p.Y < MaxY;

    private static (bool IsWin, IReadOnlyCollection<Cell> CrossedOutCells) CheckWin(Game game, Cell move, Side side)
    {
        // Horizontal --
        if (CheckDirection(game, move, side, 1, 0, out var cells))
            return (true, cells);

        // Vertical |
        if (CheckDirection(game, move, side, 0, 1, out cells))
            return (true, cells);

        // Diagonal \
        if (CheckDirection(game, move, side, 1, 1, out cells))
            return (true, cells);

        // Diagonal /
        if (CheckDirection(game, move, side, 1, -1, out cells))
            return (true, cells);

        return (false, []);
    }
}
