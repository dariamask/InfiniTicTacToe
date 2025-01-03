var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<WebSocketManager>();

var app = builder.Build();

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
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var webSocketManager = context.RequestServices.GetRequiredService<WebSocketManager>();
    var socketId = Guid.NewGuid().ToString();
    webSocketManager.AddSocket(socketId, webSocket);
    await webSocketManager.ReceiveMessagesAsync(socketId, webSocket);
});


app.MapFallbackToFile("/index.html");

app.Run();
