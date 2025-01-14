namespace SuperFlow.Core.Default.Actions.TelegramAction.Models
{
	public class TelegramConfig
	{
		public string BotApiKey { get; set; }
		public string DefaultChatId { get; set; }
	}
	public class TelegramActionParameters
	{
		public string Message { get; set; } = "Mensaje por defecto";
		public string? ChatId { get; set; }
	}
}
