using Amazon.SQS.Model;
using Moq;
using SuperFlow.Core.Default.Tools.SQSTool;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests.ToolsTest
{
	public class SQSToolTests
	{
		[Fact]
		public async Task ExecuteAsync_WithMessages_ReturnsFirstMessage()
		{
			// Arrange
			var mockSQSReceiver = new Mock<ISQSReceiver>();
			var mockMessage = new Message
			{
				MessageId = "12345",
				Body = "Test Message",
				ReceiptHandle = "handle",
				Attributes = new Dictionary<string, string>(),
				MessageAttributes = new Dictionary<string, MessageAttributeValue>()
			};
			mockSQSReceiver.Setup(receiver => receiver.ReceiveMessageAsync(It.IsAny<int>()))
						  .ReturnsAsync(mockMessage);

			var sqsTool = new SQSTool("SQSTool", mockSQSReceiver.Object);
			var context = new FlowContext();

			var parameters = new SQSToolParameters
			{
				Operation = "receive",
				ReceiveTimeoutSeconds = 10
			};

			// Act
			var result = await sqsTool.ExecuteAsync(context, parameters);

			// Assert
			var sqsResult = Assert.IsType<SQSToolResult>(result);
			Assert.Equal("12345", sqsResult.MessageId);
			Assert.Equal("Test Message", sqsResult.Body);
			mockSQSReceiver.Verify(receiver => receiver.ReceiveMessageAsync(10), Times.Once);
		}

		[Fact]
		public async Task ExecuteAsync_WithNoMessages_ReturnsNoMessage()
		{
			// Arrange
			var mockSQSReceiver = new Mock<ISQSReceiver>();
			mockSQSReceiver.Setup(receiver => receiver.ReceiveMessageAsync(It.IsAny<int>()))
						  .ReturnsAsync((Message?)null);

			var sqsTool = new SQSTool("SQSTool", mockSQSReceiver.Object);
			var context = new FlowContext();

			var parameters = new SQSToolParameters
			{
				Operation = "receive",
				ReceiveTimeoutSeconds = 10
			};

			// Act
			var result = await sqsTool.ExecuteAsync(context, parameters);

			// Assert
			var sqsResult = Assert.IsType<SQSToolResult>(result);
			Assert.Equal("No hay mensajes en la cola.", sqsResult.Body);
			Assert.Null(sqsResult.MessageId);
			mockSQSReceiver.Verify(receiver => receiver.ReceiveMessageAsync(10), Times.Once);
		}
	}
}
