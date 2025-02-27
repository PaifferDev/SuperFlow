namespace SuperFlow.Core.Default.Tools.CaptchaTool.Models
{
	public class CaptchaResult
	{
		public CaptchaResult(string providerName, string captchaText, double solveTimeSeconds, string captchaId)
		{
			ProviderName = providerName;
			CaptchaText = captchaText;
			SolveTimeSeconds = solveTimeSeconds;
			CaptchaId = captchaId;
		}
		public string ProviderName { get; }
		public string CaptchaText { get; }
		public double SolveTimeSeconds { get; }
		public string CaptchaId { get; }
	}
}
