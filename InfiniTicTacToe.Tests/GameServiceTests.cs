using System.Text.Json;

using InfiniTicTacToe.Server.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace InfiniTicTacToe.Tests;

public class GameServiceTests
{
    private readonly Mock<IWebSocketGameManager> _webSocketManagerMock;

    public GameServiceTests()
    {
        _webSocketManagerMock = new Mock<IWebSocketGameManager>();
        _ = new GameService(_webSocketManagerMock.Object, new Mock<ILogger<GameService>>().Object);
    }

    [Fact]
    public void AddPlayer_ShouldAddTwoPlayers_GameStartedAfterSecondPlayerJoin()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";

        // Act
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        // Assert that no message is sent to player1, game not started yet
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<TypedMessage>(m => m.Type != MessageType.Hello)), Times.Never);

        // Act
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyStartMessage(msg, PlayerSide.X))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyStartMessage(msg, PlayerSide.O))), Times.Once);
    }

    [Fact]
    public void MakeMove_ShouldAcceptTurn()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

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
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moveMessage1 = JsonSerializer.Serialize(new { type = "move", x = 0, y = 0, });
        var moveMessage2 = JsonSerializer.Serialize(new { type = "move", x = 0, y = 0, });

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
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (0, 1, player1Id), (1, 1, player2Id),
            (0, 2, player1Id), (1, 2, player2Id),
            (0, 3, player1Id), (1, 3, player2Id),
            (0, 4, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
    }

    private static bool VerifyStartMessage(object message, PlayerSide side)
    {
        if (message is not StartMessage start)
            return false;

        return start.Type == MessageType.Start && start.Side == side;
    }

    private static bool VerifyMoveMessage(object message, bool success, string responseMessage, int x, int y, int scoreX, int scoreO)
    {
        if (message is not MoveResultMessage move)
            return false;

        return move.Type == MessageType.MoveResult &&
               move.Success == success &&
               move.Message == responseMessage &&
               move.X == x &&
               move.Y == y &&
               move.ScoreX == scoreX &&
               move.ScoreO == scoreO;
    }

    [Fact]
    public void MakeMove_ShouldAllowMultipleMoves()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (0, 1, player1Id), (1, 1, player2Id),
            (0, 2, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            //var playerId = (x % 2 == 0) ? player1Id : player2Id;
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 2, 0, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 2, 0, 0))), Times.Once);
    }

    [Fact]
    public void MakeMove_ShouldNotAllowMoveOutOfBounds()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moveMessage = JsonSerializer.Serialize(new { type = "move", x = 9999, y = 9999 });

        // Act
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, player1Id));

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id,
            It.Is<object>(msg => VerifyMoveMessage(msg, false, "Move out of bounds.", 9999, 9999, 0, 0))), Times.Once);
    }

    [Fact]
    public void CheckWin_ShouldDetectHorizontalWin()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (0, 1, player1Id), (1, 1, player2Id),
            (0, 2, player1Id), (1, 2, player2Id),
            (0, 3, player1Id), (1, 3, player2Id),
            (0, 4, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 4, 1, 0))), Times.Once);
    }

    [Fact]
    public void CheckWin_ShouldDetectVerticalWin()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (0, 1, player2Id),
            (1, 0, player1Id), (1, 1, player2Id),
            (2, 0, player1Id), (2, 1, player2Id),
            (3, 0, player1Id), (3, 1, player2Id),
            (4, 0, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 0, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 0, 1, 0))), Times.Once);
    }

    [Fact]
    public void CheckWin_ShouldDetectDiagonalWin()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (1, 1, player1Id), (2, 0, player2Id),
            (2, 2, player1Id), (3, 0, player2Id),
            (3, 3, player1Id), (4, 0, player2Id),
            (4, 4, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 4, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 4, 1, 0))), Times.Once);
    }

    [Fact]
    public void CheckWin_ShouldDetectDiagonalWin_WhenWinSeriesWasUnordered()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (1, 1, player1Id), (2, 0, player2Id),
            (4, 4, player1Id), (3, 0, player2Id),
            (3, 3, player1Id), (4, 0, player2Id),
            (2, 2, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 2, 2, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 2, 2, 1, 0))), Times.Once);
    }

    [Fact(Skip = "Currently cross out not implemented")]
    public void CheckWin_CrossedOutCellsShouldNotBeAvailableAgain()
    {
        // Arrange
        var player1Id = "player1";
        var player2Id = "player2";
        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player1Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player1Id));

        _webSocketManagerMock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(player2Id));
        _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs("""{ "type":"hello" }""", player2Id));

        var moves = new List<(int x, int y, string playerId)>
        {
            (0, 0, player1Id), (1, 0, player2Id),
            (1, 1, player1Id), (2, 0, player2Id),
            (4, 4, player1Id), (3, 0, player2Id),
            (3, 3, player1Id), (4, 0, player2Id),
            (2, 2, player1Id), (7, 7, player2Id),
            (5, 5, player1Id),
        };

        // Act
        foreach (var (x, y, playerId) in moves)
        {
            var moveMessage = JsonSerializer.Serialize(new { type = "move", x = x, y = y });
            _webSocketManagerMock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(moveMessage, playerId));
        }

        // Assert
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 5, 5, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 5, 5, 1, 0))), Times.Once);
    }
}
