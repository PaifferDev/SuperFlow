using Moq;
using SuperFlow.Core.Default.Tools.RequestTool;
using SuperFlow.Core.Default.Tools.RequestTool.Models;
using SuperFlow.Core.Models;
using System.Net;

namespace SuperFlow.Tests.ToolsTest
{
	public class RequestToolTests
	{
		[Fact]
		public async Task ExecuteAsync_Should_ReturnResponseBody_When_GET()
		{
			// Arrange
			var mockFactory = new Mock<IHttpClientFactory>();
			var mockHttpMessageHandler = new MockHttpMessageHandler((request) =>
			{
				// Simulamos que devolvemos "Hello World" en una respuesta 200 OK
				var response = new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("Hello World")
				};
				return response;
			});

			var client = new HttpClient(mockHttpMessageHandler);
			mockFactory.Setup(f => f.CreateClient("GenericClient"))
					   .Returns(client);

			var config = new RequestToolConfig
			{
				BaseUrl = "https://example.com"
			};

			var tool = new RequestTool("HttpRequestTool", config, mockFactory.Object);

			var flowContext = new FlowContext();
			var parameters = new RequestToolParameters
			{
				Method = "GET",
				Endpoint = "test"
			};

			// Act
			var result = await tool.ExecuteAsync(flowContext, parameters);

			// Assert
			Assert.NotNull(result);
			// result debería ser { StatusCode=200, Body="Hello World" }
			int statusCode = (int)result.GetType().GetProperty("StatusCode")!.GetValue(result);
			string body = (string)result.GetType().GetProperty("Body")!.GetValue(result);

			Assert.Equal(200, statusCode);
			Assert.Equal("Hello World", body);
		}
	}

	// Mock simple de HttpMessageHandler para simular peticiones
	internal class MockHttpMessageHandler : HttpMessageHandler
	{
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _sendFunc;

		public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sendFunc)
		{
			_sendFunc = sendFunc;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = _sendFunc(request);
			return Task.FromResult(response);
		}
	}
}
