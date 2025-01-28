using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class BestCaptchaSolverProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly List<string> _apiTokens; // Lista de API tokens
		private readonly int _pollingDelayMs;
		private readonly Random _random;

		// Mapeo captchaId => token (para reportar luego)
		private readonly ConcurrentDictionary<string, string> _captchaKeyMapping;

		public string Name => "BestCaptchaSolver";
		public int Trust => 6;
		public double? AverageSolveTimeSeconds => null;
		public decimal? CostPerCaptcha => null;

		/// <summary>
		/// Constructor que acepta múltiples API tokens (multi-token).
		/// </summary>
		/// <param name="httpClient">HttpClient</param>
		/// <param name="apiTokens">Lista de tokens disponibles</param>
		/// <param name="pollingDelayMs">Retraso entre polls</param>
		public BestCaptchaSolverProvider(HttpClient httpClient, IEnumerable<string> apiTokens, int pollingDelayMs = 5000)
		{
			if (httpClient == null)
				throw new ArgumentNullException(nameof(httpClient));

			_httpClient = httpClient;
			_apiTokens = new List<string>(apiTokens ?? throw new ArgumentNullException(nameof(apiTokens)));
			if (_apiTokens.Count == 0)
				throw new ArgumentException("Se requiere al menos un token de BestCaptchaSolver.");

			_pollingDelayMs = pollingDelayMs;
			_random = new Random();
			_captchaKeyMapping = new ConcurrentDictionary<string, string>();
		}

		/// <summary>
		/// Sube el captcha (en base64) a BestCaptchaSolver usando un token al azar
		/// y luego hace polling en /get_status hasta tener la respuesta.
		/// </summary>
		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("La imagen del captcha está vacía.");

			// 1) Escoger un token al azar
			var selectedToken = _apiTokens[_random.Next(_apiTokens.Count)];

			// 2) Subir captcha en base64
			var base64Img = Convert.ToBase64String(imageData);
			var contentValues = new Dictionary<string, string>
			{
				{ "access_token", selectedToken },
				{ "b64image", base64Img }
                // se pueden añadir params extras como 'is_case=1', etc. (según docs)
            };
			var formContent = new FormUrlEncodedContent(contentValues);

			var uploadResp = await _httpClient.PostAsync("https://bcsapi.xyz/api/captcha/image", formContent, cancelToken);
			var uploadStr = await uploadResp.Content.ReadAsStringAsync(cancelToken);

			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var uploadResult = JsonSerializer.Deserialize<UploadResult>(uploadStr, options);

			if (uploadResult == null)
			{
				throw new Exception($"[BestCaptchaSolver] Error parseando upload: {uploadStr}");
			}
			// Según la doc "image" Endpoint: 
			//   la respuesta "id": <num>, "status":"submitted"
			//   => se resuelve en ese mismo POST, 
			//   y tardan un poco más en responder con "id" final
			// Revisa la doc actual: "As soon as you received ID, it's completed"
			// En la doc, dice "the limit is 10mb" y "No delay needed, we can fetch right after"
			// => ID se retorna cuando el captcha ya fue resuelto
			if (uploadResult.Status != "submitted")
			{
				// Si no dice "submitted", es un error
				throw new Exception($"[BestCaptchaSolver] Subida falló => {uploadStr}");
			}

			// A veces la doc dice que "once you got an ID, you can /get it right away"
			// => Vamos a poll en /captcha/{id}?access_token=...
			int captchaId = uploadResult.Id;
			_captchaKeyMapping[captchaId.ToString()] = selectedToken;

			// 3) Polling => en la doc de "Retrieve" dice: 
			//   GET https://bcsapi.xyz/api/captcha/{CAPTCHA_ID}?access_token={ACCESS_TOKEN}
			//   Ej: { "id":25, "text":"polum", "status":"completed" }
			//   => ya completado
			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);

				var checkUrl = $"https://bcsapi.xyz/api/captcha/{captchaId}?access_token={selectedToken}";
				var checkResp = await _httpClient.GetStringAsync(checkUrl, cancelToken);
				var checkResult = JsonSerializer.Deserialize<RetrieveResult>(checkResp, options);

				if (checkResult == null)
				{
					throw new Exception($"[BestCaptchaSolver] Error parseando retrieve => {checkResp}");
				}

				if (checkResult.Status == "completed")
				{
					// => tenemos la solución
					return new CaptchaResponse
					{
						CaptchaId = captchaId.ToString(),
						Solution = checkResult.Text
					};
				}

				if (sw.Elapsed.TotalSeconds > 120)
				{
					throw new TimeoutException($"[BestCaptchaSolver] Timeout tras 120s sin resolver captchaId={captchaId}");
				}
			}
		}

		/// <summary>
		/// Reporta el captcha como fallido usando /captcha/bad/{CAPTCHA_ID}
		/// POST con { "access_token":"..." } en body
		/// </summary>
		public async Task ReportFailureAsync(string captchaId)
		{
			if (string.IsNullOrEmpty(captchaId)) return;
			if (!_captchaKeyMapping.TryGetValue(captchaId, out var usedToken))
			{
				Log.Information($"[BestCaptchaSolver] No se encontró token para captchaId={captchaId}");
				return;
			}

			var badPayload = new Dictionary<string, string>
			{
				{ "access_token", usedToken }
			};
			var badContent = new FormUrlEncodedContent(badPayload);

			// docs => POST https://bcsapi.xyz/api/captcha/bad/{CAPTCHA_ID}
			var url = $"https://bcsapi.xyz/api/captcha/bad/{captchaId}";
			var resp = await _httpClient.PostAsync(url, badContent);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information($"[BestCaptchaSolver] ReportFailure => {respStr}");
		}

		// Clases auxiliares

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
