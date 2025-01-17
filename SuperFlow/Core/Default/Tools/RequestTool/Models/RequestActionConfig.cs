namespace SuperFlow.Core.Default.Tools.RequestTool.Models
{
	/// <summary>
	/// Configuración para RequestTool (por ejemplo, un endpoint por defecto, headers, etc.).
	/// </summary>
	public class RequestToolConfig
	{
		public string BaseUrl { get; set; }
		public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>();
	}
	public class RequestToolParameters
	{
		public string Method { get; set; } = "GET";
		public string Endpoint { get; set; } = "";
		public string? Body { get; set; }
	}
}
