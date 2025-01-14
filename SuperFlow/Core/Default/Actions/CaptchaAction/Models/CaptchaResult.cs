namespace SuperFlow.Core.Default.Actions.CaptchaAction.Models
{
	public class CaptchaResult
	{
		public string ProviderName { get; }
		public string CaptchaText { get; }
		public double SolveTimeSeconds { get; }
		public string CaptchaId { get; }

		public CaptchaResult(string providerName, string captchaText, double solveTimeSeconds, string captchaId)
		{
			ProviderName = providerName;
			CaptchaText = captchaText;
			SolveTimeSeconds = solveTimeSeconds;
			CaptchaId = captchaId;
		}
	}
}
