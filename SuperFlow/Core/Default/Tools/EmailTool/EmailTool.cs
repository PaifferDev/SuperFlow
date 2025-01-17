using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Tools.EmailTool
{
	public class EmailTool : BaseTool
	{
		private readonly EmailToolConfig _config;

		public EmailTool(string name, EmailToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as EmailToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo EmailToolParameters");

			using var client = new SmtpClient(_config.SmtpHost, _config.SmtpPort)
			{
				Credentials = new System.Net.NetworkCredential(_config.SmtpUser, _config.SmtpPass),
				EnableSsl = _config.EnableSsl
			};

			var mailMessage = new MailMessage
			{
				From = new MailAddress(_config.FromAddress),
				Subject = args.Subject,
				Body = args.Body,
				IsBodyHtml = _config.IsBodyHtml
			};

			foreach (var to in args.ToAddresses)
			{
				mailMessage.To.Add(to);
			}

			if (args.Attachments != null)
			{
				foreach (var attachment in args.Attachments)
				{
					mailMessage.Attachments.Add(new Attachment(attachment));
				}
			}

			await client.SendMailAsync(mailMessage);

			return new
			{
				Status = "Email Sent",
				To = args.ToAddresses
			};
		}
	}

	public class EmailToolConfig
	{
		public string SmtpHost { get; set; }
		public int SmtpPort { get; set; }
		public string SmtpUser { get; set; }
		public string SmtpPass { get; set; }
		public string FromAddress { get; set; }
		public bool EnableSsl { get; set; } = true;
		public bool IsBodyHtml { get; set; } = false;
	}

	public class EmailToolParameters
	{
		public List<string> ToAddresses { get; set; } = new();
		public string Subject { get; set; } = "Asunto por defecto";
		public string Body { get; set; } = "Cuerpo del correo por defecto";
		public List<string>? Attachments { get; set; }
	}
}
