using System.Diagnostics;

using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed class GameService(
    IWebSocketConnectionManager webSocketManager,
    MoveProcessor moveProcessor,
    GameStorage gameStorage,
    ILogger<GameService> logger)
{
    public async Task ProcessPlayerInfo(string socketId, ClientHelloMessage helloMessage)
    {
        gameStorage.Players.AddOrUpdate(
            key: socketId,
            addValueFactory: key => new Player(key, helloMessage.Nickname),
            updateValueFactory: (_, p) => p with { Nickname = helloMessage.Nickname });

        await webSocketManager.SendMessageAsync(socketId, new ServerHelloMessage());
    }

    public async Task StartOrCreateGame(string socketId)
    {
        var player = gameStorage.Players.TryGetValue(socketId, out var found)
            ? found
            : throw new InvalidOperationException("Player not found.");

        var pendingGame = gameStorage.Games.Values.FirstOrDefault(g => g.Status == GameStatus.Pending);
        if (pendingGame != null && pendingGame.TryStartGame())
        {
            Debug.Assert(pendingGame.PlayerX != null, "Player X is null");

            pendingGame.PlayerO = player;
            pendingGame.PlayerO.Side = Side.O;
            pendingGame.PlayerO.CurrentGame = pendingGame;
            pendingGame.CurrentPlayerId = pendingGame.PlayerX.Id;

            await webSocketManager.SendMessageAsync(pendingGame.PlayerX.Id, new StartMessage(PlayerSide.X, YourTurn: true, pendingGame.PlayerX.Nickname, pendingGame.PlayerO.Nickname));
            await webSocketManager.SendMessageAsync(pendingGame.PlayerO.Id, new StartMessage(PlayerSide.O, YourTurn: false, pendingGame.PlayerX.Nickname, pendingGame.PlayerO.Nickname));
            return;
        }

        var newGame = new Game(Guid.NewGuid().ToString())
        {
            PlayerX = player,
        };

        newGame.PlayerX.Side = Side.X;
        newGame.PlayerX.CurrentGame = newGame;
        gameStorage.Games.TryAdd(newGame.Id, newGame);

        await webSocketManager.SendMessageAsync(socketId, new ReadyMessageAck());
    }

    public async Task MakeMove(string socketId, MoveMessage moveMessage)
    {
        if (!gameStorage.Players.TryGetValue(socketId, out var player))
        {
            logger.LogError("Player not found for socket {SocketId}", socketId);
            await webSocketManager.SendMessageAsync(socketId, new MoveResultMessage(false, "Player not found.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
            return;
        }

        var game = player.CurrentGame;
        if (game == null)
        {
            logger.LogError("Game not found for player {SocketId}", socketId);
            await webSocketManager.SendMessageAsync(socketId, new MoveResultMessage(false, "Game not found.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
            return;
        }

        if (game.Status != GameStatus.InProgress)
        {
            await webSocketManager.SendMessageAsync(socketId, new MoveResultMessage(false, "Game is not in progress.", moveMessage.X, moveMessage.Y, 0, 0, [], false));
            return;
        }

        var otherPlayer = game.PlayerX?.Id == socketId
            ? game.PlayerO
            : game.PlayerX;
        Debug.Assert(otherPlayer != null, "Other player is null");

        var moveResult = moveProcessor.MakeMove(game, player, moveMessage.X, moveMessage.Y);
        if (moveResult.IsAccepted)
            game.CurrentPlayerId = otherPlayer.Id;

        var moveMessageResult = new MoveResultMessage(
            Success: moveResult.IsAccepted,
            Message: moveResult.Message,
            X: moveMessage.X,
            Y: moveMessage.Y,
            ScoreX: game.ScoreX,
            ScoreO: game.ScoreO,
            CrossedOutCells: moveResult.CrossedOutCells,
            YourTurn: false);

        await webSocketManager.SendMessageAsync(
            player.Id,
            moveMessageResult with { YourTurn = moveProcessor.IsPlayerTurn(game, player.Id) });

        await webSocketManager.SendMessageAsync(
            otherPlayer.Id,
            moveMessageResult with { YourTurn = moveProcessor.IsPlayerTurn(game, otherPlayer.Id) });
    }

    public async Task TryFinishGame(string socketId)
    {
        var player = gameStorage.Players.TryGetValue(socketId, out var found)
            ? found
            : null;

        if (player is null)
            return;

        gameStorage.Players.TryRemove(socketId, out _);

        var game = player.CurrentGame;
        if (game == null)
            return;

        game.CurrentPlayerId = null;

        // TODO #13: Implement game end logic
        if (game.Status == GameStatus.InProgress)
        {
            _ = game.TryFinishGame();

            if (game.PlayerX?.Id == socketId)
                await webSocketManager.SendMessageAsync(game.PlayerX.Id, new GameEndMessage(game.ScoreX, game.ScoreO));

            if (game.PlayerO?.Id == socketId)
                await webSocketManager.SendMessageAsync(game.PlayerO.Id, new GameEndMessage(game.ScoreX, game.ScoreO));
        }

        if (game.PlayerX == null && game.PlayerO == null)
        {
            gameStorage.Games.TryRemove(game.Id, out _);
        }
    }
}
