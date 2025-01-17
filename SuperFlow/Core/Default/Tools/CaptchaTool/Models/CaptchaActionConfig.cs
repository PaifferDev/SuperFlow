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
		/// <summary>
		/// Tiempo máximo (en segundos) para intentar resolver un captcha con un provider.
		/// </summary>
		public int SolveTimeoutSeconds { get; set; } = 70;

		/// <summary>
		/// Número máximo de reintentos (si deseas).
		/// </summary>
		public int MaxRetries { get; set; } = 3;

		/// <summary>
		/// Lista de providers disponibles para resolver captchas.
		/// </summary>
		public List<ICaptchaProvider> Providers { get; set; } = new List<ICaptchaProvider>();
	}
	public class CaptchaToolParameters
	{
		public byte[] ImageData { get; set; } = Array.Empty<byte>();
	}
}
