namespace SuperFlow.Core.Default.Tools.CaptchaTool.Models
{
	public interface ICaptchaProvider
	{
		string Name { get; }
		int Trust { get; }
		Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token = default);
		Task ReportFailureAsync(string captchaId);
	}
}
