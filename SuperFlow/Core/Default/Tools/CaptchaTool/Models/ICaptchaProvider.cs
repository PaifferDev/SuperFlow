namespace SuperFlow.Core.Default.Tools.CaptchaTool.Models
{
	public interface ICaptchaProvider
	{
		string Name { get; }
		double? AverageSolveTimeSeconds { get; }
		decimal? CostPerCaptcha { get; }

		Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token = default);
		Task ReportFailureAsync(string captchaId);
	}
}
