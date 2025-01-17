using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniTicTacToe.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InfiniTicTacToe.Tests
{
    public class WebSocketConnectionManagerTests
    {
        private readonly Mock<ILogger<WebSocketConnectionManager>> _loggerMock;
        private readonly WebSocketConnectionManager _webSocketConnectionManager;

        public WebSocketConnectionManagerTests()
        {
            _loggerMock = new Mock<ILogger<WebSocketConnectionManager>>();
            _webSocketConnectionManager = new WebSocketConnectionManager(_loggerMock.Object);
        }

        [Fact]
        public async Task SendMessageAsync_ValidIdAndMessage_SendsMessage()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>();
            socket.Setup(s => s.State).Returns(WebSocketState.Open);
            socket.Setup(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            _webSocketConnectionManager.AddSocket(socketId, socket.Object);

            var message = new { Text = "Hello, World!" };

            // Act
            await _webSocketConnectionManager.SendMessageAsync(socketId, message);

            // Assert
            socket.Verify(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_InvalidId_DoesNotSendMessage()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>();
            socket.Setup(s => s.State).Returns(WebSocketState.Open);

            _webSocketConnectionManager.AddSocket(socketId, socket.Object);

            var message = new { Text = "Hello, World!" };

            // Act
            await _webSocketConnectionManager.SendMessageAsync(Guid.NewGuid().ToString(), message);

            // Assert
            socket.Verify(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text, true, CancellationToken.None), Times.Never);
        }

        [Fact]
        public async Task ReceiveMessagesAsync_CloseMessage_RemovesSocket()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>();
            socket.Setup(s => s.State).Returns(WebSocketState.Open);
            socket.Setup(s => s.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "Closed by the client"));

            var socketFinishedTcs = new TaskCompletionSource<object>();
            var cancellationToken = CancellationToken.None;

            _webSocketConnectionManager.AddSocket(socketId, socket.Object);

            // Act
            await _webSocketConnectionManager.ReceiveMessagesAsync(socketId, socket.Object, socketFinishedTcs, cancellationToken);

            // Assert
            await socketFinishedTcs.Task;
            Assert.Null(_webSocketConnectionManager.GetSocketById(socketId));
        }

        [Fact]
        public async Task ReceiveMessagesAsync_Exception_LogsError()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>();
            socket.Setup(s => s.State).Returns(WebSocketState.Open);
            socket.Setup(s => s.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new Exception("Test exception"));

            var socketFinishedTcs = new TaskCompletionSource<object>();
            var cancellationToken = CancellationToken.None;

            _webSocketConnectionManager.AddSocket(socketId, socket.Object);

            // Act
            var task = _webSocketConnectionManager.ReceiveMessagesAsync(socketId, socket.Object, socketFinishedTcs, cancellationToken);

            // Assert
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                // Проверяем, что исключение было выброшено
                Assert.Equal("Test exception", ex.Message);
            }

            // Проверяем, что задача завершена с исключением
            Assert.True(socketFinishedTcs.Task.IsFaulted);
            Assert.Equal("Test exception", socketFinishedTcs.Task.Exception.InnerException.Message);

            // Проверяем, что метод Log вызван с правильными параметрами
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.Is<Exception>(e => e.Message == "Test exception"),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!),
                Times.Once);
        }

        [Fact]
        public void AddSocket_ValidIdAndSocket_AddsSocket()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>().Object;

            // Act
            _webSocketConnectionManager.AddSocket(socketId, socket);

            // Assert
            Assert.NotNull(_webSocketConnectionManager.GetSocketById(socketId));
        }

        [Fact]
        public void GetSocketById_ValidId_ReturnsSocket()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>().Object;
            _webSocketConnectionManager.AddSocket(socketId, socket);

            // Act
            var result = _webSocketConnectionManager.GetSocketById(socketId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(socket, result);
        }

        [Fact]
        public void GetSocketById_InvalidId_ReturnsNull()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();

            // Act
            var result = _webSocketConnectionManager.GetSocketById(socketId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RemoveSocket_ValidId_RemovesSocket()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var socket = new Mock<WebSocket>();
            socket.Setup(s => s.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);

            _webSocketConnectionManager.AddSocket(socketId, socket.Object);

            var cancellationToken = CancellationToken.None;

            // Act
            await _webSocketConnectionManager.RemoveSocket(socketId, cancellationToken);

            // Assert
            Assert.Null(_webSocketConnectionManager.GetSocketById(socketId));
            socket.Verify(s => s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the WebSocketManager", cancellationToken), Times.Once);
        }

        [Fact]
        public async Task RemoveSocket_InvalidId_DoesNotRemoveSocket()
        {
            // Arrange
            var socketId = Guid.NewGuid().ToString();
            var cancellationToken = CancellationToken.None;

            // Act
            await _webSocketConnectionManager.RemoveSocket(socketId, cancellationToken);

            // Assert
            Assert.Null(_webSocketConnectionManager.GetSocketById(socketId));
        }
    }
}
