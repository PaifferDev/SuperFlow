using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class TwoCaptchaProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly List<string> _apiKeys;
		private readonly int _pollingDelayMs;
		private readonly Random _random;
		private readonly ConcurrentDictionary<string, string> _captchaKeyMapping;

		public string Name => "2Captcha";
		public int Trust => 7;
		public double? AverageSolveTimeSeconds => null;
		public decimal? CostPerCaptcha => null;

		public TwoCaptchaProvider(HttpClient httpClient, IEnumerable<string> apiKeys, int pollingDelayMs = 5000)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_apiKeys = new List<string>(apiKeys ?? throw new ArgumentNullException(nameof(apiKeys)));
			if (_apiKeys.Count == 0)
				throw new ArgumentException("No API keys provided for 2Captcha.");
			_pollingDelayMs = pollingDelayMs;
			_random = new Random();
			_captchaKeyMapping = new ConcurrentDictionary<string, string>();
		}

		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default, bool sensitivity = false)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("Empty captcha image data.");

			var selectedKey = _apiKeys[_random.Next(_apiKeys.Count)];
			var base64Image = Convert.ToBase64String(imageData);
			var keyValues = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("method", "base64"),
				new KeyValuePair<string, string>("key", selectedKey),
				new KeyValuePair<string, string>("body", base64Image),
				new KeyValuePair<string, string>("json", "1")
			};
			if (sensitivity)
			{
				keyValues.Add(new KeyValuePair<string, string>("case", "true"));
			}
			var formContent = new FormUrlEncodedContent(keyValues);

			HttpResponseMessage uploadResponse;
			try
			{
				uploadResponse = await _httpClient.PostAsync("https://2captcha.com/in.php", formContent, cancelToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[2Captcha] Error al enviar in.php.");
				throw;
			}
			var uploadString = await uploadResponse.Content.ReadAsStringAsync(cancelToken);
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var uploadResult = JsonSerializer.Deserialize<TwoCaptchaInResponse>(uploadString, options);
			if (uploadResult == null)
				throw new Exception($"[2Captcha] No se pudo parsear la respuesta de upload: {uploadString}");
			if (uploadResult.Status != 1)
				throw new Exception($"[2Captcha] Upload falló (status != 1). Respuesta completa: {uploadString}");

			string captchaId = uploadResult.Request;
			_captchaKeyMapping[captchaId] = selectedKey;

			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);
				var checkUrl = $"https://2captcha.com/res.php?key={selectedKey}&action=get&id={captchaId}&json=1";
				string checkResponse;
				try
				{
					checkResponse = await _httpClient.GetStringAsync(checkUrl, cancelToken);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "[2Captcha] Error durante el polling.");
					throw;
				}
				var checkResult = JsonSerializer.Deserialize<TwoCaptchaResResponse>(checkResponse, options);
				if (checkResult == null)
					throw new Exception($"[2Captcha] No se pudo parsear la respuesta de polling: {checkResponse}");
				if (checkResult.Status == 1)
				{
					Log.Information("[2Captcha] Captcha resuelto para ID={CaptchaId}.", captchaId);
					return new CaptchaResponse { CaptchaId = captchaId, Solution = checkResult.Request };
				}
				else if (checkResult.Request?.StartsWith("ERROR_") == true)
				{
					throw new Exception($"[2Captcha] Error en polling: {checkResult.Request}");
				}
				if (sw.Elapsed.TotalSeconds > 120)
					throw new TimeoutException($"[2Captcha] Timeout tras 120s esperando la solución para ID={captchaId}");
			}
		}

		public async Task ReportFailureAsync(string captchaId)
		{
			if (string.IsNullOrEmpty(captchaId))
				return;
			if (!_captchaKeyMapping.TryGetValue(captchaId, out var usedKey))
			{
				Log.Information($"[2Captcha] No se encontró API key para captchaId={captchaId} al reportar fallo.");
				return;
			}
			var url = $"https://2captcha.com/res.php?key={usedKey}&action=reportbad&id={captchaId}&json=1";
			string respStr = await _httpClient.GetStringAsync(url, CancellationToken.None);
			Log.Information("[2Captcha] ReportFailure: {Response}", respStr);
		}

		private class TwoCaptchaInResponse
		{
			[JsonPropertyName("status")]
			public int Status { get; set; }
			[JsonPropertyName("request")]
			public string Request { get; set; }
		}

		private class TwoCaptchaResResponse
		{
			[JsonPropertyName("status")]
			public int Status { get; set; }
			[JsonPropertyName("request")]
			public string Request { get; set; }
		}
	}
}
