namespace SuperFlow.Core.Default.Tools.CaptchaTool.Models
{
	/// <summary>
	/// Configuración básica para la CaptchaTool.
	/// - Proveedores disponibles
	/// - Timeout
	/// - Número de reintentos, etc.
	/// </summary>
	public class CaptchaToolConfig
	{
		public int SolveTimeoutSeconds { get; set; } = 70;
		public int MaxRetries { get; set; } = 1;
		public List<ICaptchaProvider> Providers { get; set; } = new List<ICaptchaProvider>();
	}

	public class CaptchaToolParameters
	{
		public byte[] ImageData { get; set; } = Array.Empty<byte>();
		public bool Sensitivity { get; set; } = false;
	}

	public class CaptchaToolResult
	{
		public string ProviderName { get; set; }
		public string CaptchaText { get; set; }
		public double SolveTimeSeconds { get; set; }
		public string CaptchaId { get; set; }
	}

}
