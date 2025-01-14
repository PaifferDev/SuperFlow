using SuperFlow.Core.Actions;
using SuperFlow.Core.Default.Actions.TelegramAction.Models;
using SuperFlow.Core.Models;
using Telegram.Bot;

namespace SuperFlow.Core.Default.Actions.TelegramAction
{
	public class TelegramAction : BaseAction
	{
		private readonly TelegramConfig _config;

		public TelegramAction(string name, TelegramConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as TelegramActionParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo TelegramActionParameters");

			string message = args.Message;
			string chatId = args.ChatId ?? _config.DefaultChatId;
			var botClient = CreateBotClient(_config.BotApiKey);

			var response = await SendMessageAsync(botClient, chatId, message);

			return new
			{
				MessageId = response.MessageId,
				Chat = response.Chat.Id
			};
		}

		/// <summary>
		/// Método protegido que crea el botClient.
		/// </summary>
		protected virtual ITelegramBotClient CreateBotClient(string apiKey)
		{
			return new TelegramBotClient(apiKey);
		}

		/// <summary>
		/// Método protegido que llama a SendMessage.
		/// </summary>
		protected virtual Task<Telegram.Bot.Types.Message> SendMessageAsync(ITelegramBotClient botClient, string chatId, string text)
		{
			return botClient.SendMessage(chatId, text);
		}
	}
}
