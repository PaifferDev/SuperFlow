using Amazon.SQS;
using Amazon.SQS.Model;

namespace SuperFlow.Core.Default.Tools.SQSTool
{
	public class SQSReceiver : ISQSReceiver
	{
		private readonly IAmazonSQS _sqsClient;
		private readonly string _queueUrl;

		public SQSReceiver(IAmazonSQS sqsClient, SQSToolConfig config)
		{
			_sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
			if (config == null) throw new ArgumentNullException(nameof(config));
			var response = _sqsClient.GetQueueUrlAsync(config.QueueName).GetAwaiter().GetResult();
			_queueUrl = response.QueueUrl;
		}

		public async Task<Message?> ReceiveMessageAsync(int waitTimeSeconds)
		{
			var request = new ReceiveMessageRequest
			{
				QueueUrl = _queueUrl,
				WaitTimeSeconds = waitTimeSeconds,
				MaxNumberOfMessages = 1,
				MessageAttributeNames = new List<string> { "All" }
			};

			var response = await _sqsClient.ReceiveMessageAsync(request);
			return response.Messages.FirstOrDefault();
		}
	}
}
