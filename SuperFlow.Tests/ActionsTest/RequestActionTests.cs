﻿using Moq;
using SuperFlow.Core.Default.Actions.RequestAction;
using SuperFlow.Core.Default.Actions.RequestAction.Models;
using SuperFlow.Core.Models;
using System.Net;

namespace SuperFlow.Tests.ActionsTest
{
	public class RequestActionTests
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

			var config = new RequestActionConfig
			{
				BaseUrl = "https://example.com"
			};

			var action = new RequestAction("HttpRequestAction", config, mockFactory.Object);

			var flowContext = new FlowContext();
			var parameters = new RequestActionParameters
			{
				Method = "GET",
				Endpoint = "test"
			};

			// Act
			var result = await action.ExecuteAsync(flowContext, parameters);

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
