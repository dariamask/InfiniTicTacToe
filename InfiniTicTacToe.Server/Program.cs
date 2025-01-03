using InfiniTicTacToe.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IWebSocketGameManager, WebSocketGameManager>();
builder.Services.AddSingleton<GameService>();

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

//https://learn.microsoft.com/ru-ru/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0#accept-websocket-requests
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
        var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketGameManager>();
        var socketId = Guid.NewGuid().ToString();
        webSocketManager.AddSocket(socketId, webSocket);
        await webSocketManager.ReceiveMessagesAsync(socketId, webSocket, CancellationToken.None);
    }
    catch (Exception)
    {

        throw;
    }
});


app.MapFallbackToFile("/index.html");

app.Run();
