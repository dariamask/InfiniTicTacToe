using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using InfiniTicTacToe.Server.Services;

using Microsoft.Extensions.Logging;

using Moq;

namespace InfiniTicTacToe.Tests;

[SuppressMessage("Style", "IDE0037:Use inferred member name", Justification = "Explicit names for members of anonymous types")]
public class GameServiceTests
{
    private const string Player1Id = "player1";
    private const string Player2Id = "player2";

    private readonly Mock<IWebSocketConnectionManager> _webSocketManagerMock;

    public GameServiceTests()
    {
        _webSocketManagerMock = new Mock<IWebSocketConnectionManager>();
        _ = new GameService(_webSocketManagerMock.Object, new Mock<ILogger<GameService>>().Object);
    }

    [Fact]
    public async Task AddPlayer_ShouldAddTwoPlayers_GameStartedAfterSecondPlayerJoin()
    {
        // Act
        _webSocketManagerMock.WebsocketOpened(Player1Id);
        _webSocketManagerMock.MessageSent(Player1Id, new ClientHelloMessage("player1-nickname"));
        await WaitUntilMessageSent<ServerHelloMessage>(_ => true, Player1Id);

        // Act
        _webSocketManagerMock.WebsocketOpened(Player2Id);
        _webSocketManagerMock.MessageSent(Player2Id, new ClientHelloMessage("player2-nickname"));
        await WaitUntilMessageSent<ServerHelloMessage>(_ => true, Player2Id);

        _webSocketManagerMock.MessageSent(Player1Id, new ReadyMessage());
        await WaitUntilMessageSent<ReadyMessageAck>(_ => true, Player1Id);

        // Verify that no "start" messages are sent to any player
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(It.IsAny<string>(), It.IsAny<StartMessage>()), Times.Never);

        _webSocketManagerMock.MessageSent(Player2Id, new ReadyMessage());

        // Assert
        await WaitUntilMessageSent<StartMessage>(m => m.YourTurn && m.Side == PlayerSide.X, Player1Id);
        await WaitUntilMessageSent<StartMessage>(m => !m.YourTurn && m.Side == PlayerSide.O, Player2Id);
    }

    [Fact]
    public async Task MakeMove_ShouldAcceptTurn()
    {
        // Arrange
        await JoinGame();

        // Act
        _webSocketManagerMock.MessageSent(Player1Id, new MoveMessage(0, 0));

        // Assert
        await WaitUntilMessageSent<MoveResultMessage>(msg => VerifyMessage(msg, false), Player1Id);
        await WaitUntilMessageSent<MoveResultMessage>(msg => VerifyMessage(msg, true), Player2Id);

        static bool VerifyMessage(MoveResultMessage msg, bool yourTurn)
        {
            return msg.Success && msg.X == 0 && msg.Y == 0 && msg.ScoreX == 0 && msg.ScoreO == 0 && msg.YourTurn == yourTurn;
        }
    }

    [Fact]
    public async Task MakeMove_ShouldNotAllowMoveOnUsedPosition()
    {
        // Arrange
        await JoinGame();

        // Act
        _webSocketManagerMock.MessageSent(Player1Id, new MoveMessage(0, 0));
        await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success, Player1Id);

        _webSocketManagerMock.MessageSent(Player2Id, new MoveMessage(0, 0));

        // Assert
        await WaitUntilMessageSent<MoveResultMessage>(
            msg => !msg.Success && msg.Message == "Position already used.",
            Player2Id);

        Assert.True(true);
    }

    [Fact]
    public async Task CheckWin_ShouldUpdateScoreWhenPlayerWins()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (1, 0, Player2Id),
            (0, 1, Player1Id), (1, 1, Player2Id),
            (0, 2, Player1Id), (1, 2, Player2Id),
            (0, 3, Player1Id), (1, 3, Player2Id),
            (0, 4, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        Assert.True(lastMessage.Success);
        Assert.Equal(1, lastMessage.ScoreX);
        Assert.Equal(0, lastMessage.ScoreO);

        var expectedCrossedOutCells = Enumerable
            .Range(0, 5)
            .Select(i => new Cell(0, i, Side.X) { CrossedOut = true });
        foreach (var cell in expectedCrossedOutCells)
        {
            Assert.Contains(cell, lastMessage.CrossedOutCells);
        }
    }

    [Fact]
    public async Task MakeMove_ShouldAllowMultipleMoves()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (1, 0, Player2Id),
            (0, 1, Player1Id), (1, 1, Player2Id),
            (0, 2, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 2, 0, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 2, 0, 0))), Times.Once);
    }

    [Fact]
    public async Task MakeMove_ShouldNotAllowMoveOutOfBounds()
    {
        // Arrange
        await JoinGame();

        var moveMessage = new MoveMessage(9999, 9999);

        // Act
        _webSocketManagerMock.MessageSent(Player1Id, moveMessage);
        var message = await WaitUntilMessageSent<MoveResultMessage>(msg => !msg.Success, Player1Id);

        // Assert
        Assert.Equal("Move out of bounds.", message.Message);
        Assert.Equal(9999, message.X);
        Assert.Equal(9999, message.Y);
        Assert.Equal(0, message.ScoreX);
        Assert.Equal(0, message.ScoreO);
        Assert.True(message.YourTurn);
    }

    [Fact]
    public async Task CheckWin_ShouldDetectHorizontalWin()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 2, Player1Id), (1, 0, Player2Id),
            (0, 3, Player1Id), (1, 1, Player2Id),
            (0, 4, Player1Id), (1, 2, Player2Id),
            (0, 5, Player1Id), (1, 3, Player2Id),
            (0, 6, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 6, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 0, 6, 1, 0))), Times.Once);
    }

    [Fact]
    public async Task CheckWin_ShouldDetectVerticalWin()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (0, 1, Player2Id),
            (1, 0, Player1Id), (1, 1, Player2Id),
            (2, 0, Player1Id), (2, 1, Player2Id),
            (3, 0, Player1Id), (3, 1, Player2Id),
            (4, 0, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 0, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 0, 1, 0))), Times.Once);
    }

    [Fact]
    public async Task CheckWin_ShouldDetectDiagonalWin()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (1, 0, Player2Id),
            (1, 1, Player1Id), (2, 0, Player2Id),
            (2, 2, Player1Id), (3, 0, Player2Id),
            (3, 3, Player1Id), (4, 0, Player2Id),
            (4, 4, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 4, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 4, 4, 1, 0))), Times.Once);
    }

    [Fact]
    public async Task CheckWin_ShouldDetectDiagonalWin_WhenWinSeriesWasUnordered()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (1, 0, Player2Id),
            (1, 1, Player1Id), (2, 0, Player2Id),
            (4, 4, Player1Id), (3, 0, Player2Id),
            (3, 3, Player1Id), (4, 0, Player2Id),
            (2, 2, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 2, 2, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 2, 2, 1, 0))), Times.Once);
    }

    [Fact] // (Skip = "Currently cross out not implemented")]
    public async Task CheckWin_CrossedOutCellsShouldNotBeAvailableAgain()
    {
        // Arrange
        await JoinGame();

        var moves = new List<(int X, int Y, string PlayerId)>
        {
            (0, 0, Player1Id), (1, 0, Player2Id),
            (1, 1, Player1Id), (2, 0, Player2Id),
            (4, 4, Player1Id), (3, 0, Player2Id),
            (3, 3, Player1Id), (4, 0, Player2Id),
            (2, 2, Player1Id), (7, 7, Player2Id),
            (5, 5, Player1Id),
        };

        // Act
        MoveResultMessage? lastMessage = null;
        foreach (var (x, y, playerId) in moves)
        {
            _webSocketManagerMock.MessageSent(playerId, new MoveMessage(x, y));
            lastMessage = await WaitUntilMessageSent<MoveResultMessage>(msg => msg.Success && msg.X == x && msg.Y == y, playerId);
        }

        // Assert
        Assert.NotNull(lastMessage);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player1Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 5, 5, 1, 0))), Times.Once);
        _webSocketManagerMock.Verify(m => m.SendMessageAsync(Player2Id, It.Is<object>(msg => VerifyMoveMessage(msg, true, "Move accepted.", 5, 5, 1, 0))), Times.Once);
    }

    private async Task JoinGame()
    {
        _webSocketManagerMock.WebsocketOpened(Player1Id);
        _webSocketManagerMock.MessageSent(Player1Id, new ClientHelloMessage("player1-nickname"));
        await WaitUntilMessageSent<ServerHelloMessage>(_ => true, Player1Id);

        _webSocketManagerMock.WebsocketOpened(Player2Id);
        _webSocketManagerMock.MessageSent(Player2Id, new ClientHelloMessage("player2-nickname"));
        await WaitUntilMessageSent<ServerHelloMessage>(_ => true, Player2Id);

        _webSocketManagerMock.MessageSent(Player1Id, new ReadyMessage());
        await WaitUntilMessageSent<ReadyMessageAck>(_ => true, Player1Id);

        _webSocketManagerMock.MessageSent(Player2Id, new ReadyMessage());
        await WaitUntilMessageSent<StartMessage>(m => m.YourTurn && m.Side == PlayerSide.X, Player1Id);
        await WaitUntilMessageSent<StartMessage>(m => !m.YourTurn && m.Side == PlayerSide.O, Player2Id);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeout = 1000)
    {
        if (condition())
            return;

        var start = Stopwatch.StartNew();
        while (start.ElapsedMilliseconds < timeout)
        {
            await Task.Delay(10);
            if (condition())
                return;
        }

        throw new TimeoutException();
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

    private async Task<TMessage> WaitUntilMessageSent<TMessage>(Func<TMessage, bool> condition, string playerId, int timeout = 1000)
    {
        TMessage? message = default;
        await WaitUntil(
            () =>
            {
                var suitableMessage = _webSocketManagerMock.Invocations
                    .Where(x => x.Method.Name == "SendMessageAsync" && x.Arguments[0].ToString() == playerId)
                    .Select(x => x.Arguments[1])
                    .OfType<TMessage>()
                    .FirstOrDefault(message => condition(message));

                if (suitableMessage is not null)
                {
                    message = suitableMessage;
                    return true;
                }

                return false;
            },
            timeout);

        return message ?? throw new TimeoutException();
    }
}

internal static class SocketManagerExtensions
{
    // Mock<IWebSocketConnectionManager>

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public static void WebsocketOpened(this Mock<IWebSocketConnectionManager> mock, string id)
    {
        mock.Raise(m => m.ConnectionReceived += null, new object(), new WebsocketConnectionEventArgs(id));
    }

    public static void MessageSent<T>(this Mock<IWebSocketConnectionManager> mock, string id, T message)
    {
        var messageJson = JsonSerializer.Serialize(message, _options);
        mock.Raise(m => m.MessageReceived += null, new object(), new WebsocketMessageEventArgs(messageJson, id));
    }
}
