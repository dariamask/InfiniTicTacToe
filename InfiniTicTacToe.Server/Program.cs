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

app.UseMiddleware<WebSocketMiddleware>();

app.MapFallbackToFile("/index.html");

app.Run();
