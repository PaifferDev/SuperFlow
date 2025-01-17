using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace SuperFlow.Core.Default.Tools.SMSProviderTool
{
	public class SMSProviderTool : BaseTool
	{
		private readonly SMSProviderToolConfig _config;

		public SMSProviderTool(string name, SMSProviderToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			TwilioClient.Init(_config.AccountSid, _config.AuthToken);
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as SMSProviderToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo SMSProviderToolParameters");

			var message = await MessageResource.CreateAsync(
				body: args.Message,
				from: new Twilio.Types.PhoneNumber(_config.FromNumber),
				to: new Twilio.Types.PhoneNumber(args.ToNumber)
			);

			return new
			{
				SID = message.Sid,
				Status = message.Status.ToString()
			};
		}
	}

	public class SMSProviderToolConfig
	{
		public string AccountSid { get; set; }
		public string AuthToken { get; set; }
		public string FromNumber { get; set; }
	}

	public class SMSProviderToolParameters
	{
		public string ToNumber { get; set; }
		public string Message { get; set; } = "Mensaje por defecto";
	}
}
