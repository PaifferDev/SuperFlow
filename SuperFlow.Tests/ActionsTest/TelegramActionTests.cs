using SuperFlow.Core.Default.Actions.TelegramAction;
using SuperFlow.Core.Default.Actions.TelegramAction.Models;
using SuperFlow.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
namespace SuperFlow.Tests.ActionsTest
{
	public class TelegramActionTests
	{
		[Fact]
		public async Task ExecuteAsync_Should_SendMessageCorrectly()
		{
			// Arrange
			var config = new TelegramConfig
			{
				BotApiKey = "FAKE_APIKEY",
				DefaultChatId = "12345"
			};

			var action = new FakeTelegramAction("SendTelegramAction", config);

			var flowContext = new FlowContext();
			var parameters = new TelegramActionParameters
			{
				Message = "Hola Mundo",
				ChatId = "999999"
			};

			// Act
			var result = await action.ExecuteAsync(flowContext, parameters);

			// Assert
			Assert.NotNull(result);

			// result => { MessageId, Chat }
			var messageIdProp = result.GetType().GetProperty("MessageId");
			var chatIdProp = result.GetType().GetProperty("Chat");

			int messageId = (int)messageIdProp.GetValue(result);
			long chatId = (long)chatIdProp.GetValue(result);

			Assert.Equal(111, messageId);   // Valor fake
			Assert.Equal(999999, chatId);   // Valor proveniente del Step
		}
	}

	// ------------------------------------------------------------------------
	// FakeTelegramAction: sobrescribe CreateBotClient y SendMessageAsync
	// ------------------------------------------------------------------------
	internal class FakeTelegramAction : TelegramAction
	{
		public FakeTelegramAction(string name, TelegramConfig config)
			: base(name, config)
		{
		}

		protected override ITelegramBotClient CreateBotClient(string apiKey)
		{
			// No llamamos a new TelegramBotClient real. 
			// Retornamos un Fake/Mock/lo que desees
			return new FakeTelegramBotClient();
		}

		protected override Task<Message> SendMessageAsync(ITelegramBotClient botClient, string chatId, string text)
		{
			var msg = new Message
			{
				Id = 111,
				Chat = new Chat { Id = long.Parse(chatId) },
				Text = text
			};
			return Task.FromResult(msg);
		}

		// Fake que implementa ITelegramBotClient, pero sin nada real
		private class FakeTelegramBotClient : ITelegramBotClient
		{
			public event AsyncEventHandler<ApiRequestEventArgs>? OnMakingApiRequest;
			public event AsyncEventHandler<ApiResponseEventArgs>? OnApiResponseReceived;
			public string GetBotToken() => "FAKE_TOKEN";
			public Task<Message> SendMessage(string chatId, string text) =>
				throw new NotImplementedException("No se llama directamente porque se sobrescribe en SendMessageAsync(...)");

			public bool LocalBotServer
			{
				get => throw new NotImplementedException();
				set => throw new NotImplementedException();
			}

			public TimeSpan Timeout
			{
				get => throw new NotImplementedException();
				set => throw new NotImplementedException();
			}

			public long BotId => throw new NotImplementedException();

			public IExceptionParser ExceptionsParser
			{
				get => throw new NotImplementedException();
				set => throw new NotImplementedException();
			}

			public Task DownloadFileAsync(string filePath, Stream destination, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task<TResponse> MakeRequestAsync<TResponse>(Telegram.Bot.Requests.RequestBase<TResponse> request, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task<TResponse> MakeRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task<TResponse> MakeRequestAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task<bool> TestApi(CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();

			public Task DownloadFile(string filePath, Stream destination, CancellationToken cancellationToken = default)
				=> throw new NotImplementedException();
		}

	}
}
