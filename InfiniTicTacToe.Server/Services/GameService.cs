using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniTicTacToe.Server.Services;

public enum GameStatus
{
    Pending,
    InProgress,
    Finished,
}

public sealed record Player(string Id, char Symbol, string Nickname);

public sealed class Game(string id)
{
    public string Id { get; } = id;

    public ConcurrentDictionary<string, Player> Players { get; } = new();

    public GameStatus Status { get; set; } = GameStatus.Pending;

    public HashSet<(int X, int Y)> UsedPositions { get; } = [];

    // вместо массива чаров - словарик, в котором ключ это котреж координат ху, а значение объект (статус ячейки - х, о, порядковый номер хода, флаг IsCrossedOut).
    public char[,] Board { get; } = new char[GameService.MaxX, GameService.MaxY];

    public string? CurrentPlayerId { get; set; }

    public int ScoreX { get; set; }

    public int ScoreO { get; set; }
}

public sealed class GameService : IDisposable
{
    internal const int MaxX = 100;
    internal const int MaxY = 100;

    private readonly IWebSocketConnectionManager _webSocketManager;
    private readonly ILogger<GameService> _logger;

    // private readonly ConcurrentDictionary<string, Player> _players = new();
    private readonly ConcurrentDictionary<string, Game> _games = new();

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
        _webSocketManager.ConnectionReceived += OnConnectionReceived;
        _webSocketManager.ConnectionClosed += OnConnectionClosed;
    }

    public void Dispose()
    {
        _webSocketManager.MessageReceived -= OnMessageReceived;
        _webSocketManager.ConnectionReceived -= OnConnectionReceived;
        _webSocketManager.ConnectionClosed -= OnConnectionClosed;
    }

    private void OnConnectionReceived(object? sender, WebsocketConnectionEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await OnConnectionReceivedImpl(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing connection");
            }
        });
    }

    private async Task OnConnectionReceivedImpl(WebsocketConnectionEventArgs e)
    {
        await _webSocketManager.SendMessageAsync(e.SocketId, new ServerHelloMessage());
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
        foreach (var game in _games.Values)
        {
            if (game.Players.TryRemove(e.SocketId, out var player))
            {
                game.CurrentPlayerId = null;

                if (game.Status == GameStatus.InProgress)
                {
                    foreach (var playerId in game.Players.Keys)
                    {
                        await _webSocketManager.SendMessageAsync(playerId, new GameEndMessage(game.ScoreX, game.ScoreO));
                    }

                    game.Status = GameStatus.Finished;
                }

                if (game.Players.IsEmpty)
                {
                    _games.TryRemove(game.Id, out _);
                }

                break;
            }
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
        if (messageData != null)
        {
            if (messageData.Type == MessageType.ClientHello)
            {
                // QuestionForAndo: получается, hello теперь отправляем только одному игроку,
                // т.к. у него нет ещё нет оппонента.
                await _webSocketManager.SendMessageAsync(e.SocketId, new StartMessage(PlayerSide.X, true));
            }
            else if (messageData.Type == MessageType.Ready)
            {
                var pendingGame = _games.Values.FirstOrDefault(g => g.Status == GameStatus.Pending);
                if (pendingGame != null)
                {
                    char symbol = pendingGame.Players.IsEmpty ? 'X' : 'O';
                    pendingGame.Players.TryAdd(e.SocketId, new Player(e.SocketId, symbol, "nickname"));
                    if (pendingGame.Players.Count == 2)
                    {
                        pendingGame.Status = GameStatus.InProgress;
                        pendingGame.CurrentPlayerId = pendingGame.Players.Where(x => x.Value.Symbol == 'X').Select(x => x.Key).FirstOrDefault()
                            ?? throw new InvalidOperationException("Player X not found.");

                        var playerX = pendingGame.Players.Where(x => x.Value.Symbol == 'X').Select(x => x.Key).FirstOrDefault()
                            ?? throw new InvalidOperationException("Player X not found.");
                        var playerO = pendingGame.Players.Where(x => x.Value.Symbol == 'O').Select(x => x.Key).FirstOrDefault()
                            ?? throw new InvalidOperationException("Player O not found.");

                        await _webSocketManager.SendMessageAsync(playerX, new StartMessage(PlayerSide.X, true));
                        await _webSocketManager.SendMessageAsync(playerO, new StartMessage(PlayerSide.O, false));

                        pendingGame.Status = GameStatus.InProgress;
                    }
                }
                else
                {
                    // TODO: передать никнейм
                    var newGame = new Game(Guid.NewGuid().ToString());
                    newGame.Players.TryAdd(e.SocketId, new Player(e.SocketId, 'X', "nickname"));
                    _games.TryAdd(newGame.Id, newGame);
                }
            }
            else if (messageData.Type == MessageType.Move)
            {
                var moveMessage = JsonSerializer.Deserialize<MoveMessage>(e.Message, _jsonSerializerOptions)!;

                var game = _games.Values.FirstOrDefault(g => g.Players.ContainsKey(e.SocketId));
                if (game == null)
                {
                    _logger.LogError("Game not found for player {SocketId}", e.SocketId);
                    return;
                }

                var (success, responseMessage) = MakeMove(game, e.SocketId, moveMessage.X, moveMessage.Y);
                var (scoreX, scoreO) = GetScores(game);
                var moveMessageResult = new MoveResultMessage(success, responseMessage, moveMessage.X, moveMessage.Y, scoreX, scoreO, false);

                foreach (var playerId in game.Players.Keys)
                {
                    if (playerId == game.CurrentPlayerId)
                    {
                        await _webSocketManager.SendMessageAsync(playerId, moveMessageResult with { YourTurn = true });
                    }
                    else
                    {
                        await _webSocketManager.SendMessageAsync(playerId, moveMessageResult);
                    }
                }
            }
        }
    }

    private static bool IsPlayerTurn(Game game, string id) => game.CurrentPlayerId == id;

    private static (bool Success, string Message) MakeMove(Game game, string id, int x, int y)
    {
        if (game.Status != GameStatus.InProgress)
        {
            return (false, "Game is not in progress.");
        }

        if (!IsPlayerTurn(game, id))
        {
            return (false, "It's not your turn.");
        }

        if (!IsValidPosition(x, y))
        {
            return (false, "Move out of bounds.");
        }

        if (game.UsedPositions.Contains((x, y)))
        {
            return (false, "Position already used.");
        }

        var player = game.Players[id];
        game.Board[x, y] = player.Symbol;
        game.UsedPositions.Add((x, y));

        if (CheckWin(game, x, y, player.Symbol))
        {
            if (player.Symbol == 'X')
            {
                game.ScoreX++;
            }
            else
            {
                game.ScoreO++;
            }
        }

        game.CurrentPlayerId = game.Players.Keys.First(k => k != id);
        return (true, "Move accepted.");
    }

    private static (int ScoreX, int ScoreO) GetScores(Game game)
    {
        int scoreX = game.Players.Values.Count(p => p.Symbol == 'X');
        int scoreO = game.Players.Values.Count(p => p.Symbol == 'O');
        return (scoreX, scoreO);
    }

    private static bool CheckWin(Game game, int x, int y, char symbol)
    {
        return CheckDirection(game, x, y, symbol, 1, 0) || // Horizontal
               CheckDirection(game, x, y, symbol, 0, 1) || // Vertical
               CheckDirection(game, x, y, symbol, 1, 1) || // Diagonal \
               CheckDirection(game, x, y, symbol, 1, -1);  // Diagonal /
    }

    private static bool CheckDirection(Game game, int x, int y, char symbol, int dx, int dy)
    {
        int count = 1;
        for (int i = 1; i < 5; i++)
        {
            if (IsValidPosition(x + (i * dx), y + (i * dy)) && game.Board[x + (i * dx), y + (i * dy)] == symbol)
            {
                count++;
            }
            else
            {
                break;
            }
        }

        for (int i = 1; i < 5; i++)
        {
            if (IsValidPosition(x - (i * dx), y - (i * dy)) && game.Board[x - (i * dx), y - (i * dy)] == symbol)
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count >= 5;
    }

    private static bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < MaxX && y < MaxY;
}
