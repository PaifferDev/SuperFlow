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

		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("Empty captcha image data.");

			// Seleccionar una API key al azar
			var selectedKey = _apiKeys[_random.Next(_apiKeys.Count)];

			// 1) Convertir la imagen a Base64
			var base64Image = Convert.ToBase64String(imageData);

			// 2) Construir el contenido de la solicitud
			var uploadContent = new FormUrlEncodedContent(new[]
			{
		new KeyValuePair<string, string>("method", "base64"),
		new KeyValuePair<string, string>("key", selectedKey),
		new KeyValuePair<string, string>("body", base64Image),
		new KeyValuePair<string, string>("json", "1"),
	});

			// 3) Enviar la solicitud al servidor de 2Captcha
			var uploadResponse = await _httpClient.PostAsync("https://2captcha.com/in.php", uploadContent, cancelToken);
			var uploadString = await uploadResponse.Content.ReadAsStringAsync(cancelToken);

			// 4) Verificar la respuesta del servidor
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var uploadResult = JsonSerializer.Deserialize<TwoCaptchaInResponse>(uploadString, options);

			if (uploadResult == null)
				throw new Exception($"[2Captcha] Could not parse upload response: {uploadString}");

			if (uploadResult.Status != 1)
			{
				throw new Exception($"[2Captcha] Upload failed (status !=1). Full response: {uploadString}");
			}

			string captchaId = uploadResult.Request; 
			_captchaKeyMapping[captchaId] = selectedKey;

			// 5) Iniciar el proceso de polling
			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);

				var checkUrl = $"https://2captcha.com/res.php?key={selectedKey}&action=get&id={captchaId}&json=1";
				var checkResponse = await _httpClient.GetStringAsync(checkUrl, cancelToken);

				var checkResult = JsonSerializer.Deserialize<TwoCaptchaResResponse>(checkResponse, options);
				if (checkResult == null)
					throw new Exception($"[2Captcha] Could not parse polling response: {checkResponse}");

				if (checkResult.Status == 1)
				{
					// Éxito, devolver la solución
					return new CaptchaResponse
					{
						CaptchaId = captchaId,
						Solution = checkResult.Request
					};
				}
				else if (checkResult.Request?.StartsWith("ERROR_") == true)
				{
					throw new Exception($"[2Captcha] Error in polling => {checkResult.Request}");
				}

				if (sw.Elapsed.TotalSeconds > 120)
				{
					throw new TimeoutException($"[2Captcha] Timed out after 120s waiting for captcha solution. ID={captchaId}");
				}
			}
		}


		public async Task ReportFailureAsync(string captchaId)
		{
			if (string.IsNullOrEmpty(captchaId))
				return;

			if (!_captchaKeyMapping.TryGetValue(captchaId, out var usedKey))
			{
				Log.Information($"[2Captcha] Could not find API key for captchaId={captchaId} to report failure.");
				return;
			}

			var url = $"https://2captcha.com/res.php?key={usedKey}&action=reportbad&id={captchaId}&json=1";
			var resp = await _httpClient.GetStringAsync(url);
			Log.Information($"[2Captcha] ReportFailure => {resp}");
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
