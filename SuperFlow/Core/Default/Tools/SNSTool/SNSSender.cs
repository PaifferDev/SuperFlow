using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace SuperFlow.Core.Default.Tools.SNSTool
{
	public class SNSSender : ISNSSender
	{
		private readonly IAmazonSimpleNotificationService _snsClient;
		private readonly string _topicArn;

		public SNSSender(IAmazonSimpleNotificationService snsClient, SNSToolConfig config)
		{
			_snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
			if (config == null) throw new ArgumentNullException(nameof(config));

			var response = _snsClient.CreateTopicAsync(config.TopicName).GetAwaiter().GetResult();
			_topicArn = response.TopicArn;
		}

		public async Task<string> PublishMessageAsync(string message, string subject = "")
		{
			var request = new PublishRequest
			{
				TopicArn = _topicArn,
				Message = message,
				Subject = subject
			};

			var response = await _snsClient.PublishAsync(request);
			return response.MessageId;
		}
	}
}
