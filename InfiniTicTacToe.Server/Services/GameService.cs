using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InfiniTicTacToe.Server.Services;

public sealed class GameService : IDisposable
{
    private const int MaxX = 100;
    private const int MaxY = 100;

    private readonly IWebSocketConnectionManager _webSocketManager;
    private readonly ILogger<GameService> _logger;
    private readonly ConcurrentDictionary<string, Player> _players = new();
    private readonly char[,] _board = new char[MaxX, MaxY];
    private readonly HashSet<(int, int)> _usedPositions = [];

    private string? _currentPlayerId;

    private int _scoreX;
    private int _scoreO;

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
        _webSocketManager.SendMessageAsync(e.SocketId, new ServerHelloMessage()).Wait();

        if (_players.Count < 2)
        {
            char symbol = _players.IsEmpty ? 'X' : 'O';
            _players.TryAdd(e.SocketId, new Player(e.SocketId, symbol));
            if (_players.Count == 2)
            {
                _currentPlayerId = _players.Where(x => x.Value.Symbol == 'X').Select(x => x.Key).FirstOrDefault()
                    ?? throw new InvalidOperationException("Player X not found.");
            }
        }
    }

    private void OnConnectionClosed(object? sender, WebsocketConnectionEventArgs e)
    {
        var isGameEnd = _players.Count == 2;

        if (_players.TryRemove(e.SocketId, out var player))
        {
            _currentPlayerId = null;
            _scoreX = 0;
            _scoreO = 0;
            Array.Clear(_board, 0, _board.Length);
            _usedPositions.Clear();

            if (isGameEnd)
            {
                foreach (var playerId in _players.Keys)
                {
                    _webSocketManager.SendMessageAsync(playerId, new GameEndMessage(_scoreX, _scoreO));
                }
            }

        }
    }

    // asyncronously handle messages without blocking the main execution loop for current web socket
    private void OnMessageReceived(object? sender, WebsocketMessageEventArgs e)
    {
        var task = Task.Run(async () =>
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

        // TODO remove
        task.Wait();
    }

    private async Task OnMessageReceivedImpl(WebsocketMessageEventArgs e)
    {
        var messageData = JsonSerializer.Deserialize<TypedMessage>(e.Message, _jsonSerializerOptions);
        if (messageData != null)
        {
            if (messageData.Type == MessageType.ClientHello && _players.Count == 2)
            {
                var playerX = _players.Where(x => x.Value.Symbol == 'X').Select(x => x.Key).FirstOrDefault()
                     ?? throw new InvalidOperationException("Player X not found.");
                var playerO = _players.Where(x => x.Value.Symbol == 'O').Select(x => x.Key).FirstOrDefault()
                     ?? throw new InvalidOperationException("Player O not found.");

                await _webSocketManager.SendMessageAsync(playerX, new StartMessage(PlayerSide.X, true));
                await _webSocketManager.SendMessageAsync(playerO, new StartMessage(PlayerSide.O, false));
            }
            else if (messageData.Type == MessageType.Move)
            {
                var moveMessage = JsonSerializer.Deserialize<MoveMessage>(e.Message, _jsonSerializerOptions)!;

                var (success, responseMessage) = MakeMove(e.SocketId, moveMessage.X, moveMessage.Y);
                var (scoreX, scoreO) = GetScores();
                var moveMessageResult = new MoveResultMessage(success, responseMessage, moveMessage.X, moveMessage.Y, scoreX, scoreO, false);
                foreach (var playerId in _players.Keys)
                {
                    if (playerId == _currentPlayerId)
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

    private bool IsPlayerTurn(string id) => _currentPlayerId == id;

    private (bool success, string message) MakeMove(string id, int x, int y)
    {
        if (!IsPlayerTurn(id))
        {
            return (false, "It's not your turn.");
        }

        if (!IsValidPosition(x, y))
        {
            return (false, "Move out of bounds.");
        }

        if (_usedPositions.Contains((x, y)))
        {
            return (false, "Position already used.");
        }

        var player = _players[id];
        _board[x, y] = player.Symbol;
        _usedPositions.Add((x, y));

        if (CheckWin(x, y, player.Symbol))
        {
            if (player.Symbol == 'X')
            {
                _scoreX++;
            }
            else
            {
                _scoreO++;
            }
        }

        _currentPlayerId = _players.Keys.First(k => k != id);
        return (true, "Move accepted.");
    }

    private (int scoreX, int scoreO) GetScores() => (_scoreX, _scoreO);

    private bool CheckWin(int x, int y, char symbol)
    {
        return CheckDirection(x, y, symbol, 1, 0) || // Horizontal
               CheckDirection(x, y, symbol, 0, 1) || // Vertical
               CheckDirection(x, y, symbol, 1, 1) || // Diagonal \
               CheckDirection(x, y, symbol, 1, -1);  // Diagonal /
    }

    private bool CheckDirection(int x, int y, char symbol, int dx, int dy)
    {
        int count = 1;
        for (int i = 1; i < 5; i++)
        {
            if (IsValidPosition(x + i * dx, y + i * dy) && _board[x + i * dx, y + i * dy] == symbol)
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
            if (IsValidPosition(x - i * dx, y - i * dy) && _board[x - i * dx, y - i * dy] == symbol)
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

    private sealed record Player(string Id, char Symbol);
}