using Amazon.SQS.Model;
using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;

namespace SuperFlow.Core.Default.Tools.SQSTool
{
	/// <summary>
	/// SQSTool
	/// </summary>
	/// <seealso cref="SuperFlow.Core.Tools.BaseTool" />
	public class SQSTool : BaseTool
	{
		/// <summary>
		/// The SQS receiver
		/// </summary>
		private readonly ISQSReceiver _sqsReceiver;
		/// <summary>
		/// Initializes a new instance of the <see cref="SQSTool"/> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="sqsReceiver">The SQS receiver.</param>
		public SQSTool(string name, ISQSReceiver sqsReceiver) : base(name)
		{
			_sqsReceiver = sqsReceiver;
		}
		/// <summary>
		/// Executes the asynchronous.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="parameters">The parameters.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">Se requieren parámetros de tipo SQSToolParameters</exception>
		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as SQSToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo SQSToolParameters");

			var message = await _sqsReceiver.ReceiveMessageAsync(args.ReceiveTimeoutSeconds);

			if (message != null)
			{
				return new SQSToolResult
				{
					MessageId = message.MessageId,
					Body = message.Body,
					Attributes = message.Attributes,
					MessageAttributes = message.MessageAttributes
				};
			}
			else
			{
				return new SQSToolResult
				{
					Body = "No hay mensajes en la cola."
				};
			}
		}
	}
	public class SQSToolResult
	{
		/// <summary>
		/// ID del mensaje recibido.
		/// </summary>
		public string? MessageId { get; set; }

		/// <summary>
		/// Cuerpo del mensaje recibido.
		/// </summary>
		public string Body { get; set; }

		/// <summary>
		/// Atributos del mensaje.
		/// </summary>
		public Dictionary<string, string> Attributes { get; set; }

		/// <summary>
		/// Atributos adicionales del mensaje.
		/// </summary>
		public Dictionary<string, MessageAttributeValue> MessageAttributes { get; set; }
	}
	public class SQSToolConfig
	{
		/// <summary>
		/// Gets or sets the name of the queue.
		/// </summary>
		/// <value>
		/// The name of the queue.
		/// </value>
		public string QueueName { get; set; } = "default_queue";
	}
	/// <summary>
	/// 
	/// </summary>
	public class SQSToolParameters
	{
		/// <summary>
		/// Gets or sets the operation.
		/// </summary>
		/// <value>
		/// The operation.
		/// </value>
		public string Operation { get; set; } = "receive";
		/// <summary>
		/// Gets or sets the receive timeout seconds.
		/// </summary>
		/// <value>
		/// The receive timeout seconds.
		/// </value>
		public int ReceiveTimeoutSeconds { get; set; } = 10;
	}
}
