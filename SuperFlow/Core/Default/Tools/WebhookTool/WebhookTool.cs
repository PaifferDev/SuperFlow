using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Tools.WebhookTool
{
	public class WebhookTool : BaseTool
	{
		private readonly WebhookToolConfig _config;
		private readonly HttpClient _httpClient;

		public WebhookTool(string name, WebhookToolConfig config, HttpClient httpClient) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			_httpClient = httpClient;
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as WebhookToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo WebhookToolParameters");

			var content = new StringContent(args.Payload, Encoding.UTF8, "application/json");
			var response = await _httpClient.PostAsync(args.Url, content);

			return new
			{
				StatusCode = (int)response.StatusCode,
				ReasonPhrase = response.ReasonPhrase
			};
		}
	}

	public class WebhookToolConfig
	{
		// Configuraciones generales si son necesarias
	}

	public class WebhookToolParameters
	{
		public string Url { get; set; }
		public string Payload { get; set; }
	}
}
