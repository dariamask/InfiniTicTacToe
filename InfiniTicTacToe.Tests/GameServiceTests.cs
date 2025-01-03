using System.Text.Json;

using InfiniTicTacToe.Server.Services;

using Moq;

namespace InfiniTicTacToe.Tests;

public class GameServiceTests
{
    private readonly Mock<IWebSocketGameManager> _webSocketManagerMock;
    private readonly GameService _gameService;

    public GameServiceTests()
    {
        _webSocketManagerMock = new Mock<IWebSocketGameManager>();
        _gameService = new GameService(_webSocketManagerMock.Object);
    }

    [Fact]
    public void AddPlayer_ShouldAddTwoPlayers()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";

        // Act
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyStartMessage(msg, player1Id, player2Id))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyStartMessage(msg, player1Id, player2Id))), Times.Once);
    }

    [Fact]
    public void MakeMove_ShouldUpdateBoardAndSwitchTurn()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));

        var moveMessage = JsonSerializer.Serialize(new { type = "move", x = 0, y = 0 });

        // Act
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, player1Id));

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 0, 0, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 0, 0, 0))), Times.Once);
    }

    [Fact]
    public void MakeMove_ShouldNotAllowMoveOnUsedPosition()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));

        var moveMessage1 = JsonSerializer.Serialize(new { type = "move", x = 0, y = 0 });
        var moveMessage2 = JsonSerializer.Serialize(new { type = "move", x = 0, y = 0 });

        // Act
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage1, player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage2, player2Id));

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, false, "Position already used.", 0, 0, 0, 0))), Times.Once);
    }

    [Fact]
    public void CheckWin_ShouldUpdateScoreWhenPlayerWins()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));

        var moves = new List<(int x, int y)>
            {
                (0, 0), (1, 0), (0, 1), (1, 1), (0, 2), (1, 2), (0, 3), (1, 3), (0, 4)
            };

        foreach (var (x, y) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x.ToString(), y = y.ToString() });
            var playerId = (x % 2 == 0) ? player1Id : player2Id;
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
    }

    private bool VerifyStartMessage(object message, string playerX, string playerO)
    {
        var json = JsonSerializer.Serialize(message);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return dict != null &&
               dict["type"] == "start" &&
               dict["playerX"] == playerX &&
               dict["playerO"] == playerO;
    }

    private bool VerifyMoveMessage(object message, bool success, string responseMessage, int x, int y, int scoreX, int scoreO)
    {
        var json = JsonSerializer.Serialize(message);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        return dict != null &&
               dict["type"].ToString() == "move" &&
               ((JsonElement)dict["success"]).ValueKind == (success ? JsonValueKind.True : JsonValueKind.False) &&
               dict["message"].ToString() == responseMessage &&
               ((JsonElement)dict["x"]).GetInt32() == x &&
               ((JsonElement)dict["y"]).GetInt32() == y &&
               ((JsonElement)dict["scoreX"]).GetInt32() == scoreX &&
               ((JsonElement)dict["scoreO"]).GetInt32() == scoreO;
    }
}
