using SuperFlow.Core.Tools;
using SuperFlow.Core.Default.Tools.RequestTool.Models;
using SuperFlow.Core.Models;
using System.Text;

public class RequestTool : BaseTool
{
	private readonly RequestToolConfig _config;
	private readonly IHttpClientFactory _httpClientFactory;

	public RequestTool(string name, RequestToolConfig config, IHttpClientFactory httpClientFactory)
		: base(name)
	{
		_config = config;
		_httpClientFactory = httpClientFactory;
	}

	// Cambia 'object?' => 'dynamic?'
	public override async Task<object?> ExecuteAsync(FlowContext context, object? parameters = null)
	{

		var args = parameters as RequestToolParameters;
		if (args == null)
		{
			throw new ArgumentException("Se requieren parámetros de tipo RequestToolParameters");
		}
		string method = args.Method ?? "GET";
		string endpoint = args.Endpoint ?? "";

		var client = _httpClientFactory.CreateClient("GenericClient");

		// Combinas con _config.BaseUrl
		string url = _config.BaseUrl.TrimEnd('/') + "/" + endpoint;

		HttpResponseMessage response;
		if (method.ToUpperInvariant() == "POST")
		{
			// Ejemplo: si tienes Body
			string body = args.Body ?? "{}";
			var content = new StringContent(body, Encoding.UTF8, "application/json");
			response = await client.PostAsync(url, content);
		}
		else
		{
			// Asume GET
			response = await client.GetAsync(url);
		}

		// Lee respuesta
		string responseBody = await response.Content.ReadAsStringAsync();
		return new
		{
			StatusCode = (int)response.StatusCode,
			Body = responseBody
		};
	}
}
