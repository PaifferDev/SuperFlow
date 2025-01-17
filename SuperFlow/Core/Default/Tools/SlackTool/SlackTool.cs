using Slack.Webhooks;
using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;

namespace SuperFlow.Core.Default.Tools.SlackTool
{
	public class SlackTool : BaseTool
	{
		private readonly SlackToolConfig _config;
		private readonly SlackClient _client;

		public SlackTool(string name, SlackToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			_client = new SlackClient(_config.WebhookUrl);
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as SlackToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo SlackToolParameters");

			var slackMessage = new SlackMessage
			{
				Channel = args.Channel,
				Text = args.Text,
				Username = args.Username ?? _config.DefaultUsername,
				IconEmoji = args.IconEmoji ?? _config.DefaultIconEmoji
			};

			if (args.Attachments != null)
			{
				slackMessage.Attachments = args.Attachments.Select(a => new SlackAttachment
				{
					Fallback = a.Fallback,
					Text = a.Text,
					Color = a.Color
				}).ToList();
			}

			bool result = await _client.PostAsync(slackMessage);
			return new
			{
				Status = result ? "Message Sent" : "Failed to Send Message"
			};
		}
	}

	public class SlackToolConfig
	{
		public string WebhookUrl { get; set; }
		public string? DefaultUsername { get; set; }
		public string? DefaultIconEmoji { get; set; }
	}

	public class SlackToolParameters
	{
		public string Text { get; set; } = "Mensaje por defecto";
		public string? Channel { get; set; }
		public string? Username { get; set; }
		public string? IconEmoji { get; set; }
		public List<SlackAttachment>? Attachments { get; set; }
	}
}
