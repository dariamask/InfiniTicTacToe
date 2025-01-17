using InfiniTicTacToe.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.UseHttpsRedirection();

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

    try
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socketFinishedTcs = new TaskCompletionSource<object>();

        var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketConnectionManager>();
        var socketId = Guid.NewGuid().ToString();
        await webSocketManager.ReceiveMessagesAsync(socketId, webSocket, socketFinishedTcs, CancellationToken.None);
        await socketFinishedTcs.Task;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        throw;
    }
});

app.MapFallbackToFile("/index.html");

app.Run();
