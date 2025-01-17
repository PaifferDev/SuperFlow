using Moq;
using SuperFlow.Core.Default.Tools.SNSTool;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests.ToolsTest
{
	public class SNSToolTests
	{
		[Fact]
		public async Task ExecuteAsync_WithValidParameters_PublishesMessageSuccessfully()
		{
			// Arrange
			var mockSNSSender = new Mock<ISNSSender>();
			mockSNSSender.Setup(sender => sender.PublishMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
						.ReturnsAsync("mock-message-id");

			var snsTool = new SNSTool("SNSTool", mockSNSSender.Object);
			var context = new FlowContext();

			var parameters = new SNSToolParameters
			{
				Message = "Test Message",
				Subject = "Test Subject"
			};

			// Act
			var result = await snsTool.ExecuteAsync(context, parameters);

			// Assert
			var snsResult = Assert.IsType<SNSToolResult>(result);
			Assert.Equal("mock-message-id", snsResult.MessageId);
			mockSNSSender.Verify(sender => sender.PublishMessageAsync("Test Message", "Test Subject"), Times.Once);
		}
	}
}
