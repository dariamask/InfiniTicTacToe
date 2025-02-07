using System.Net.WebSockets;

namespace InfiniTicTacToe.Server.Services
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebSocketMiddleware> _logger;

        public WebSocketMiddleware(RequestDelegate next, ILogger<WebSocketMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
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
                        _logger.LogInformation("WebSocket connection was canceled.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling WebSocket connection.");
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
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
