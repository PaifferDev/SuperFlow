namespace SuperFlow.Core.Default.Actions.CaptchaAction.Models
{
	/// <summary>
	/// Configuración básica para la CaptchaAction.
	/// - Proveedores disponibles
	/// - Timeout
	/// - Número de reintentos, etc.
	/// </summary>
	public class CaptchaActionConfig
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
	public class CaptchaActionParameters
	{
		public byte[] ImageData { get; set; } = Array.Empty<byte>();
	}
}
