using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniTicTacToe.Server.Services;

public enum GameStatus
{
    Pending,
    InProgress,
    Finished,
}

public sealed record Player(string Id, string Nickname)
{
    public Game? CurrentGame { get; set; }

    public Side? Side { get; set; }
}

public enum Side
{
    X,
    O,
}

public sealed record Cell(int X, int Y, Side Side)
{
    public int X { get; init; } = X;

    public int Y { get; init; } = Y;

    public Side Side { get; init; } = Side;

    public int? TurnNumber { get; set; }

    public bool CrossedOut { get; set; }

    public char Symbol => Side switch
    {
        Side.X => 'X',
        Side.O => 'O',
        _ => ' ',
    };
}

public sealed record Game(string Id)
{
    private int _statusInt = (int)GameStatus.Pending;

    public string Id { get; } = Id;

    public Player? PlayerX { get; set; }

    public Player? PlayerO { get; set; }

    public GameStatus Status => (GameStatus)_statusInt;

    // public HashSet<(int X, int Y)> UsedPositions { get; } = [];

    // вместо массива чаров - словарик, в котором ключ это котреж координат ху, а значение объект (статус ячейки - х, о, порядковый номер хода, флаг IsCrossedOut).
    // public char[,] Board { get; } = new char[GameService.MaxX, GameService.MaxY];

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

public sealed record GameStatistics(int Games, int Players, IReadOnlyCollection<GameScore> GameScores);

public sealed record GameScore(string GameId, int X, int O);

public sealed class GameService : IDisposable
{
    internal const int MaxX = 100;
    internal const int MaxY = 100;

    private readonly IWebSocketConnectionManager _webSocketManager;
    private readonly ILogger<GameService> _logger;

    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, Player> _players = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public GameService(IWebSocketConnectionManager webSocketManager, ILogger<GameService> logger)
    {
        _webSocketManager = webSocketManager;
        _logger = logger;

        _webSocketManager.MessageReceived += OnMessageReceived;
        _webSocketManager.ConnectionClosed += OnConnectionClosed;
    }

    public void Dispose()
    {
        _webSocketManager.MessageReceived -= OnMessageReceived;
        _webSocketManager.ConnectionClosed -= OnConnectionClosed;
    }

    public GameStatistics GetStats()
    {
            // _games.Values.SelectMany(g => g.Players.Values).Count(),

        return new GameStatistics(
            _games.Count,
            _players.Count,
            _games.Values.Select(g => new GameScore(g.Id, g.ScoreX, g.ScoreO)).ToList());
    }

    private void OnConnectionClosed(object? sender, WebsocketConnectionEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await OnConnectionClosedImpl(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing connection closed");
            }
        });
    }

    private async Task OnConnectionClosedImpl(WebsocketConnectionEventArgs e)
    {
        var player = _players.TryGetValue(e.SocketId, out var found)
            ? found
            : null;

        if (player is null)
            return;

        _players.TryRemove(e.SocketId, out _);

        var game = player.CurrentGame;
        if (game == null)
            return;

        game.CurrentPlayerId = null;

        // TODO #13: Implement game end logic
        if (game.Status == GameStatus.InProgress)
        {
            _ = game.TryFinishGame();

            if (game.PlayerX?.Id == e.SocketId)
                await _webSocketManager.SendMessageAsync(game.PlayerX.Id, new GameEndMessage(game.ScoreX, game.ScoreO));

            if (game.PlayerO?.Id == e.SocketId)
                await _webSocketManager.SendMessageAsync(game.PlayerO.Id, new GameEndMessage(game.ScoreX, game.ScoreO));
        }

        if (game.PlayerX == null && game.PlayerO == null)
        {
            _games.TryRemove(game.Id, out _);
        }
    }

    // asyncronously handle messages without blocking the main execution loop for current web socket
    private void OnMessageReceived(object? sender, WebsocketMessageEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await OnMessageReceivedImpl(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
            }
        });
    }

    private async Task OnMessageReceivedImpl(WebsocketMessageEventArgs e)
    {
        var messageData = JsonSerializer.Deserialize<TypedMessage>(e.Message, _jsonSerializerOptions);
        if (messageData == null)
            return;

        if (messageData.Type == MessageType.ClientHello)
        {
            var helloMessage = JsonSerializer.Deserialize<ClientHelloMessage>(e.Message, _jsonSerializerOptions)!;

            _players.AddOrUpdate(
                key: e.SocketId,
                addValueFactory: _ => new Player(e.SocketId, helloMessage.Nickname),
                updateValueFactory: (_, p) => p with { Nickname = helloMessage.Nickname });

            await _webSocketManager.SendMessageAsync(e.SocketId, new ServerHelloMessage());

            // QuestionForAndo: получается, hello теперь отправляем только одному игроку,
            // т.к. у него нет ещё нет оппонента.
            // await _webSocketManager.SendMessageAsync(e.SocketId, new StartMessage(PlayerSide.X, true));

            return;
        }

        if (messageData.Type == MessageType.Ready)
        {
            var player = _players.TryGetValue(e.SocketId, out var found)
                ? found
                : throw new InvalidOperationException("Player not found.");

            var pendingGame = _games.Values.FirstOrDefault(g => g.Status == GameStatus.Pending);
            if (pendingGame != null && pendingGame.TryStartGame())
            {
                Debug.Assert(pendingGame.PlayerX != null, "Player X is null");

                pendingGame.PlayerO = player;
                pendingGame.PlayerO.Side = Side.O;
                pendingGame.PlayerO.CurrentGame = pendingGame;
                pendingGame.CurrentPlayerId = pendingGame.PlayerX.Id;

                await _webSocketManager.SendMessageAsync(pendingGame.PlayerX.Id, new StartMessage(PlayerSide.X, YourTurn: true, pendingGame.PlayerX.Nickname, pendingGame.PlayerO.Nickname));
                await _webSocketManager.SendMessageAsync(pendingGame.PlayerO.Id, new StartMessage(PlayerSide.O, YourTurn: false, pendingGame.PlayerX.Nickname, pendingGame.PlayerO.Nickname));
                return;
            }

            var newGame = new Game(Guid.NewGuid().ToString())
            {
                PlayerX = player,
            };

            newGame.PlayerX.Side = Side.X;
            newGame.PlayerX.CurrentGame = newGame;
            _games.TryAdd(newGame.Id, newGame);

            await _webSocketManager.SendMessageAsync(e.SocketId, new ReadyMessageAck());
            return;
        }

        if (messageData.Type == MessageType.Move)
        {
            var moveMessage = JsonSerializer.Deserialize<MoveMessage>(e.Message, _jsonSerializerOptions)!;

            if (!_players.TryGetValue(e.SocketId, out var player))
            {
                _logger.LogError("Player not found for socket {SocketId}", e.SocketId);
                await _webSocketManager.SendMessageAsync(e.SocketId, new MoveResultMessage(false, "Player not found.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
                return;
            }

            var game = player.CurrentGame;
            if (game == null)
            {
                _logger.LogError("Game not found for player {SocketId}", e.SocketId);
                await _webSocketManager.SendMessageAsync(e.SocketId, new MoveResultMessage(false, "Game not found.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
                return;
            }

            if (game.Status != GameStatus.InProgress)
            {
                await _webSocketManager.SendMessageAsync(e.SocketId, new MoveResultMessage(false, "Game is not in progress.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
                return;
            }

            var otherPlayer = game.PlayerX?.Id == e.SocketId
                ? game.PlayerO
                : game.PlayerX;
            Debug.Assert(otherPlayer != null, "Other player is null");

            var moveResult = MakeMove(game, player, moveMessage.X, moveMessage.Y);
            if (moveResult.IsAccepted)
                game.CurrentPlayerId = otherPlayer.Id;

            var moveMessageResult = new MoveResultMessage(
                Success: moveResult.IsAccepted,
                Message: moveResult.ErrorMessage,
                X: moveMessage.X,
                Y: moveMessage.Y,
                ScoreX: game.ScoreX,
                ScoreO: game.ScoreO,
                CrossedOutCells: moveResult.CrossedOutCells,
                YourTurn: false);

            await _webSocketManager.SendMessageAsync(
                player.Id,
                moveMessageResult with { YourTurn = IsPlayerTurn(game, player.Id) });

            await _webSocketManager.SendMessageAsync(
                otherPlayer.Id,
                moveMessageResult with { YourTurn = IsPlayerTurn(game, otherPlayer.Id) });
        }
    }

    private static bool IsPlayerTurn(Game game, string id) => game.CurrentPlayerId == id;

    private static MoveResult MakeMove(Game game, Player player, int x, int y)
    {
        if (game.Status != GameStatus.InProgress)
            return new(false, false, [], "Game is not in progress.");

        if (!IsPlayerTurn(game, player.Id))
            return new(false, false, [], "It's not your turn.");

        if (!IsValidPosition((x, y)))
            return new(false, false, [], "Move out of bounds.");

        var playerSide = player.Side ?? throw new InvalidOperationException("Player side is not set.");

        var move = new Cell(x, y, playerSide);
        var moveSuccess = game.Board.TryAdd((x, y), move);

        if (!moveSuccess)
            return new(false, false, [], "Position already used.");

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

        return new(true, isWin, crossedOutCells, "Move accepted.");
    }

    private sealed record MoveResult(bool IsAccepted, bool IsWin, IReadOnlyCollection<Cell> CrossedOutCells, string ErrorMessage);

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
}
