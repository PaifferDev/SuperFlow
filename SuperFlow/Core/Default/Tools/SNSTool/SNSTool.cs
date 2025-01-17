using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;

namespace SuperFlow.Core.Default.Tools.SNSTool
{
	/// <summary>
	/// SNSTool
	/// </summary>
	/// <seealso cref="SuperFlow.Core.Tools.BaseTool" />
	public class SNSTool : BaseTool
	{
		/// <summary>
		/// The SNS sender
		/// </summary>
		private readonly ISNSSender _snsSender;
		/// <summary>
		/// Initializes a new instance of the <see cref="SNSTool"/> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="snsSender">The SNS sender.</param>
		public SNSTool(string name, ISNSSender snsSender) : base(name)
		{
			_snsSender = snsSender;
		}
		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as SNSToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo SNSToolParameters");

			var messageId = await _snsSender.PublishMessageAsync(args.Message, args.Subject);

			return new SNSToolResult
			{
				MessageId = messageId
			};
		}
	}
	public class SNSToolResult
	{
		public string MessageId { get; set; }
	}
	public class SNSToolConfig
	{
		/// <summary>
		/// Gets or sets the name of the topic.
		/// </summary>
		/// <value>
		/// The name of the topic.
		/// </value>
		public string TopicName { get; set; } = "default_topic";
	}
	/// <summary>
	/// 
	/// </summary>
	public class SNSToolParameters
	{
		/// <summary>
		/// Gets or sets the message.
		/// </summary>
		/// <value>
		/// The message.
		/// </value>
		public string Message { get; set; } = string.Empty;
		/// <summary>
		/// Gets or sets the subject.
		/// </summary>
		/// <value>
		/// The subject.
		/// </value>
		public string Subject { get; set; } = string.Empty;
	}
	/// <summary>
	/// 
	/// </summary>
	public interface ISNSSender
	{
		/// <summary>
		/// Publishes the message asynchronous.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="subject">The subject.</param>
		/// <returns></returns>
		Task<string> PublishMessageAsync(string message, string subject = "");
	}
}
