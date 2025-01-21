using System.Net.WebSockets;
using InfiniTicTacToe.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
builder.Services.AddSingleton<GameService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

_ = app.Services.GetRequiredService<GameService>();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseWebSockets();

app.MapControllers();

app.MapHealthChecks("/health");

// https://learn.microsoft.com/ru-ru/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0#accept-websocket-requests
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var cancellationToken = context.RequestAborted;
    string? socketId = null;
    try
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socketFinishedTcs = new TaskCompletionSource<object>();

        var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
        socketId = Guid.NewGuid().ToString();
        webSocketManager.AddSocket(socketId, webSocket);
        await webSocketManager.ReceiveMessagesAsync(socketId, webSocket, socketFinishedTcs, cancellationToken);
        await socketFinishedTcs.Task;
    }
    catch (OperationCanceledException)
    {
        app.Logger.LogInformation("WebSocket connection was canceled.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error handling WebSocket connection.");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Internal Server Error.");
    }
    finally
    {
        if (context.WebSockets.IsWebSocketRequest && socketId != null)
        {
            var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
            var webSocket = webSocketManager.GetSocketById(socketId);

            if (webSocket != null && webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }

            await webSocketManager.RemoveSocket(socketId, cancellationToken);
        }
    }
});

app.MapFallbackToFile("/index.html");

app.Run();
