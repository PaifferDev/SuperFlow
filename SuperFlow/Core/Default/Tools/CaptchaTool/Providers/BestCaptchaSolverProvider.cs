using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class BestCaptchaSolverProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly List<string> _apiTokens;
		private readonly int _pollingDelayMs;
		private readonly Random _random;
		private readonly ConcurrentDictionary<int, string> _taskKeyMapping;

		public string Name => "BestCaptchaSolver";
		public int Trust => 6;
		public double? AverageSolveTimeSeconds => null;
		public decimal? CostPerCaptcha => null;

		public BestCaptchaSolverProvider(HttpClient httpClient, IEnumerable<string> apiTokens, int pollingDelayMs = 5000)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_apiTokens = new List<string>(apiTokens ?? throw new ArgumentNullException(nameof(apiTokens)));
			if (_apiTokens.Count == 0)
				throw new ArgumentException("No API keys provided for BestCaptchaSolver.");
			_pollingDelayMs = pollingDelayMs;
			_random = new Random();
			_taskKeyMapping = new ConcurrentDictionary<int, string>();
		}

		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default, bool sensitivity = false)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("Empty captcha image data.");

			var selectedKey = _apiTokens[_random.Next(_apiTokens.Count)];
			var base64Image = Convert.ToBase64String(imageData);
			var contentValues = new Dictionary<string, string>
			{
				{ "access_token", selectedKey },
				{ "b64image", base64Image }
			};
			if (sensitivity)
			{
				contentValues["is_case"] = "1";
			}
			var formContent = new FormUrlEncodedContent(contentValues);

			HttpResponseMessage uploadResp;
			try
			{
				uploadResp = await _httpClient.PostAsync("https://bcsapi.xyz/api/captcha/image", formContent, cancelToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[BestCaptchaSolver] Error en upload.");
				throw;
			}
			var uploadStr = await uploadResp.Content.ReadAsStringAsync(cancelToken);
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var uploadResult = JsonSerializer.Deserialize<UploadResult>(uploadStr, options);
			if (uploadResult == null)
				throw new Exception($"[BestCaptchaSolver] No se pudo parsear la respuesta de upload: {uploadStr}");
			if (uploadResult.Status != "submitted")
				throw new Exception($"[BestCaptchaSolver] Upload falló. Respuesta completa: {uploadStr}");

			int captchaId = uploadResult.Id;
			_taskKeyMapping[captchaId] = selectedKey;

			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);
				var checkUrl = $"https://bcsapi.xyz/api/captcha/{captchaId}?access_token={selectedKey}";
				string checkResp = await _httpClient.GetStringAsync(checkUrl, cancelToken);
				var checkResult = JsonSerializer.Deserialize<RetrieveResult>(checkResp, options);
				if (checkResult == null)
					throw new Exception($"[BestCaptchaSolver] No se pudo parsear retrieve: {checkResp}");
				if (checkResult.Status == "completed")
				{
					Log.Information("[BestCaptchaSolver] Captcha resuelto para ID={CaptchaId}.", captchaId);
					return new CaptchaResponse { CaptchaId = captchaId.ToString(), Solution = checkResult.Text };
				}
				if (sw.Elapsed.TotalSeconds > 120)
					throw new TimeoutException($"[BestCaptchaSolver] Timeout tras 120s esperando la solución. ID={captchaId}");
			}
		}

		public async Task ReportFailureAsync(string captchaId)
		{
			if (string.IsNullOrEmpty(captchaId))
				return;
			if (!int.TryParse(captchaId, out int taskId))
				return;
			if (!_taskKeyMapping.TryGetValue(taskId, out var usedKey))
			{
				Log.Information($"[BestCaptchaSolver] No se encontró API key para captchaId={captchaId}.");
				return;
			}
			var badPayload = new Dictionary<string, string>
			{
				{ "access_token", usedKey }
			};
			var badContent = new FormUrlEncodedContent(badPayload);
			var url = $"https://bcsapi.xyz/api/captcha/bad/{captchaId}";
			var resp = await _httpClient.PostAsync(url, badContent);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information("[BestCaptchaSolver] ReportFailure: {Response}", respStr);
		}

		private class UploadResult
		{
			[JsonPropertyName("id")]
			public int Id { get; set; }
			[JsonPropertyName("status")]
			public string Status { get; set; }
		}

		private class RetrieveResult
		{
			[JsonPropertyName("id")]
			public long Id { get; set; }
			[JsonPropertyName("text")]
			public string Text { get; set; }
			[JsonPropertyName("status")]
			public string Status { get; set; }
		}
	}
}
