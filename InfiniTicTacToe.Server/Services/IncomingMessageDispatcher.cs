using System.Text.Json;
using System.Text.Json.Serialization;

using InfiniTicTacToe.Server.Models;

namespace InfiniTicTacToe.Server.Services;

public sealed class IncomingMessageDispatcher : IDisposable
{
    private readonly IWebSocketConnectionManager _webSocketManager;
    private readonly ILogger<IncomingMessageDispatcher> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public IncomingMessageDispatcher(
        IWebSocketConnectionManager webSocketManager,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<IncomingMessageDispatcher> logger)
    {
        _webSocketManager = webSocketManager;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        _webSocketManager.MessageReceived += OnMessageReceived;
        _webSocketManager.ConnectionClosed += OnConnectionClosed;
    }

    public void Dispose()
    {
        _webSocketManager.MessageReceived -= OnMessageReceived;
        _webSocketManager.ConnectionClosed -= OnConnectionClosed;
    }

    private void OnConnectionClosed(object? sender, WebsocketConnectionEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var serviceScope = _serviceScopeFactory.CreateScope();
                var gameService = serviceScope.ServiceProvider.GetRequiredService<GameService>();
                await gameService.TryFinishGame(e.SocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing connection closed");
            }
        });
    }

    // asyncronously handle messages without blocking the main execution loop for current web socket
    private void OnMessageReceived(object? sender, WebsocketMessageEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var serviceScope = _serviceScopeFactory.CreateScope();
                var gameService = serviceScope.ServiceProvider.GetRequiredService<GameService>();
                await OnMessageReceivedImpl(e, gameService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
            }
        });
    }

    private async Task OnMessageReceivedImpl(WebsocketMessageEventArgs e, GameService gameService)
    {
        var messageData = JsonSerializer.Deserialize<TypedMessage>(e.Message, _jsonSerializerOptions);
        if (messageData == null)
            return;

        if (messageData.Type == MessageType.ClientHello)
        {
            var helloMessage = JsonSerializer.Deserialize<ClientHelloMessage>(e.Message, _jsonSerializerOptions)!;
            await gameService.ProcessPlayerInfo(e.SocketId, helloMessage);
            return;
        }

        if (messageData.Type == MessageType.Ready)
        {
            await gameService.StartOrCreateGame(e.SocketId);
            return;
        }

        if (messageData.Type == MessageType.Move)
        {
            var moveMessage = JsonSerializer.Deserialize<MoveMessage>(e.Message, _jsonSerializerOptions)!;
            await gameService.MakeMove(e.SocketId, moveMessage);
        }
    }
}
