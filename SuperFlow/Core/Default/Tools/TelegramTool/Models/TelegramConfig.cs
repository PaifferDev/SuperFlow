namespace SuperFlow.Core.Default.Tools.TelegramTool.Models
{
	public class TelegramConfig
	{
		public string BotApiKey { get; set; }
		public string DefaultChatId { get; set; }
	}
	public class TelegramToolParameters
	{
		public string Message { get; set; } = "Mensaje por defecto";
		public string? ChatId { get; set; }
	}
}
