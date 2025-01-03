using System.Collections.Concurrent;
using System.Text.Json;

namespace InfiniTicTacToe.Server.Services
{
    public class GameService
    {
        private readonly IWebSocketGameManager _webSocketManager;
        private readonly ConcurrentDictionary<string, Player> _players = new();
        private readonly char[,] _board = new char[100, 100];
        private readonly HashSet<(int, int)> _usedPositions = new();
        private string? _currentPlayerId;
        private int _scoreX;
        private int _scoreO;

        public GameService(IWebSocketGameManager webSocketManager)
        {
            _webSocketManager = webSocketManager;
            _webSocketManager.MessageReceived += OnMessageReceived;
            _webSocketManager.ConnectionReceived += OnConnectionReceived;
        }

        private void OnConnectionReceived(object? sender, WebsocketConnectionEventArgs e)
        {
            if (_players.Count < 2)
            {
                char symbol = _players.Count == 0 ? 'X' : 'O';
                _players.TryAdd(e.SocketId, new Player(e.SocketId, symbol));
                if (_players.Count == 2)
                {
                    _currentPlayerId = _players.Keys.First();
                }
            }
        }

        private void OnMessageReceived(object? sender, WebsocketMessageEventArgs e)
        {
            var messageData = JsonSerializer.Deserialize<Dictionary<string, string>>(e.Message);
            if (messageData != null && messageData.TryGetValue("type", out var type))
            {
                if (type == "hello" && _players.Count == 2)
                {
                    var playerIds = _players.Keys.ToArray();
                    var startMessage = new { type = "start", playerX = playerIds[0], playerO = playerIds[1] };
                    foreach (var playerId in playerIds)
                    {
                        _webSocketManager.SendMessageAsync(playerId, startMessage).Wait();
                    }
                }
                else if (type == "move" 
                    && messageData.TryGetValue("x", out var xStr) 
                    && messageData.TryGetValue("y", out var yStr) 
                    && int.TryParse(xStr, out var x) 
                    && int.TryParse(yStr, out var y))
                {
                    var (success, responseMessage) = MakeMove(e.SocketId, x, y);
                    var scores = GetScores();
                    var moveMessage = new { type = "move", success, message = responseMessage, x, y, scoreX = scores.scoreX, scoreO = scores.scoreO };
                    foreach (var playerId in _players.Keys)
                    {
                        _webSocketManager.SendMessageAsync(playerId, moveMessage).Wait();
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

        private bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < 100 && y < 100;

        private class Player
        {
            public string Id { get; }
            public char Symbol { get; }

            public Player(string id, char symbol)
            {
                Id = id;
                Symbol = symbol;
            }
        }
    }
}