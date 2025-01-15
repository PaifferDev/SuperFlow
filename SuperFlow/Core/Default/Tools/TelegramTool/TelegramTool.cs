using SuperFlow.Core.Tools;
using SuperFlow.Core.Default.Tools.TelegramTool.Models;
using SuperFlow.Core.Models;
using Telegram.Bot;

namespace SuperFlow.Core.Default.Tools.TelegramTool
{
	public class TelegramTool : BaseTool
	{
		private readonly TelegramConfig _config;

		public TelegramTool(string name, TelegramConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as TelegramToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo TelegramToolParameters");

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
