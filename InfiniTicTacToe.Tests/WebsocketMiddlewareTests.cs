using System;
using System.Data;
using System.Net.WebSockets;
using InfiniTicTacToe.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfiniTicTacToe.Tests
{
    public class WebSocketMiddlewareTests
    {
        [Fact]
        public async Task ValidWebSocketRequest_ShouldAcceptAndHandleConnection()
        {
            // Arrange
            var webSocketManagerMock = new Mock<IWebSocketConnectionManager>();
            var loggerMock = new Mock<ILogger<WebSocketMiddleware>>();

            var testServer = new TestServer(new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(webSocketManagerMock.Object);
                    services.AddSingleton(loggerMock.Object);
                })
                .Configure(app =>
                {
                    app.UseWebSockets();
                    app.UseMiddleware<WebSocketMiddleware>();
                }));

            var webSocketClient = testServer.CreateWebSocketClient();

            var wsUri = new UriBuilder(testServer.BaseAddress)
            {
                Scheme = "ws",
                Path = "ws",
            }.Uri;

            // Act
            var websocket = await webSocketClient.ConnectAsync(wsUri, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.Equal(WebSocketState.Open, websocket.State);
            webSocketManagerMock.Verify(m => m.AddSocket(It.IsAny<string>(), It.IsAny<WebSocket>()), Times.Once);
        }

        [Fact]
        public async Task NonWebSocketRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var testServer = new TestServer(new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(Mock.Of<IWebSocketConnectionManager>());
                    services.AddSingleton(Mock.Of<ILogger<WebSocketMiddleware>>());
                })
                .Configure(app =>
                {
                    app.UseWebSockets();
                    app.UseMiddleware<WebSocketMiddleware>();
                }));

            var client = testServer.CreateClient();

            // Act
            var response = await client.GetAsync("/ws");

            // Assert
            Assert.Equal(400, (int)response.StatusCode);
        }

        [Fact]
        public async Task WebSocketConnectionError_ShouldLogErrorAndReturn500()
        {
            // Arrange
            var webSocketManagerMock = new Mock<IWebSocketConnectionManager>();
            var loggerMock = new Mock<ILogger<WebSocketMiddleware>>();

            webSocketManagerMock.Setup(m => m.ReceiveMessagesAsync(It.IsAny<string>(), It.IsAny<WebSocket>(), It.IsAny<TaskCompletionSource<object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            var testServer = new TestServer(new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(webSocketManagerMock.Object);
                    services.AddSingleton(loggerMock.Object);
                })
                .Configure(app =>
                {
                    app.UseWebSockets();
                    app.UseMiddleware<WebSocketMiddleware>();
                }));

            var webSocketClient = testServer.CreateWebSocketClient();

            var wsUri = new UriBuilder(testServer.BaseAddress)
            {
                Scheme = "ws",
                Path = "ws",
            }.Uri;

            // Act
            await webSocketClient.ConnectAsync(wsUri, CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
                Times.Once);

            webSocketManagerMock.Verify(m => m.RemoveSocket(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
