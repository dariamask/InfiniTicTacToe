using System.Net.WebSockets;
using InfiniTicTacToe.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
builder.Services.AddSingleton<IncomingMessageDispatcher>();
builder.Services.AddSingleton<GameStorage>();

builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<MoveProcessor>();

builder.Services.AddHealthChecks();

var app = builder.Build();

// ensure subscriptions are performed
_ = app.Services.GetRequiredService<IncomingMessageDispatcher>();

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
