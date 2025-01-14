namespace SuperFlow.Core.Default.Actions.RequestAction.Models
{
	/// <summary>
	/// Configuración para RequestAction (por ejemplo, un endpoint por defecto, headers, etc.).
	/// </summary>
	public class RequestActionConfig
	{
		public string BaseUrl { get; set; }
		public Dictionary<string, string> DefaultHeaders { get; set; } = new Dictionary<string, string>();
	}
	public class RequestActionParameters
	{
		public string Method { get; set; } = "GET";
		public string Endpoint { get; set; } = "";
		public string? Body { get; set; }
	}
}
