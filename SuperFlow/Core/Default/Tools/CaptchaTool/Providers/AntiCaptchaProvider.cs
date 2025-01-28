using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class AntiCaptchaProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly List<string> _clientKeys;
		private readonly int _pollingDelayMs;
		private readonly Random _random;
		private readonly ConcurrentDictionary<int, string> _taskKeyMapping;

		public string Name => "AntiCaptcha";
		public int Trust => 9;
		public double? AverageSolveTimeSeconds => null;
		public decimal? CostPerCaptcha => null;

		public AntiCaptchaProvider(HttpClient httpClient, IEnumerable<string> clientKeys, int pollingDelayMs = 5000)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_clientKeys = new List<string>(clientKeys ?? throw new ArgumentNullException(nameof(clientKeys)));
			if (_clientKeys.Count == 0)
				throw new ArgumentException("No AntiCaptcha client keys provided.");

			_pollingDelayMs = pollingDelayMs;
			_random = new Random();
			_taskKeyMapping = new ConcurrentDictionary<int, string>();
		}

		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("Empty captcha image data.");

			var selectedKey = _clientKeys[_random.Next(_clientKeys.Count)];


			// 1) createTask
			var base64Img = Convert.ToBase64String(imageData);
			var createPayload = new
			{
				clientKey = selectedKey,
				task = new
				{
					type = "ImageToTextTask",
					body = base64Img
				}
			};

			var createJson = JsonSerializer.Serialize(createPayload);
			using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

			var createResponse = await _httpClient.PostAsync("https://api.anti-captcha.com/createTask", createContent, cancelToken);
			var createString = await createResponse.Content.ReadAsStringAsync(cancelToken);

			// Usamos opciones con case-insensitive (por si hay variaciones)
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};

			var createResult = JsonSerializer.Deserialize<CreateTaskResponse>(createString, options);
			if (createResult == null)
			{
				throw new Exception($"[AntiCaptcha] Could not parse createTask response: {createString}");
			}

			if (createResult.ErrorId != 0)
			{
				throw new Exception($"[AntiCaptcha] createTask Error: {createResult.ErrorCode} - {createResult.ErrorDescription}");
			}

			if (createResult.TaskId == 0)
			{
				// Teóricamente no debería pasar si errorId=0 => success
				throw new Exception($"[AntiCaptcha] createTask gave TaskId=0 => {createString}");
			}

			int taskId = createResult.TaskId;
			_taskKeyMapping[taskId] = selectedKey;

			// 2) getTaskResult => poll until ready
			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);

				var getPayload = new
				{
					clientKey = selectedKey,
					taskId = taskId
				};
				var getJson = JsonSerializer.Serialize(getPayload);
				using var getContent = new StringContent(getJson, Encoding.UTF8, "application/json");

				var getResp = await _httpClient.PostAsync("https://api.anti-captcha.com/getTaskResult", getContent, cancelToken);
				var getStringResp = await getResp.Content.ReadAsStringAsync(cancelToken);

				var getResult = JsonSerializer.Deserialize<GetTaskResultResponse>(getStringResp, options);
				if (getResult == null)
				{
					throw new Exception($"[AntiCaptcha] Could not parse getTaskResult => {getStringResp}");
				}

				if (getResult.ErrorId != 0)
				{
					throw new Exception($"[AntiCaptcha] getTaskResult Error: {getResult.ErrorCode} - {getResult.ErrorDescription}");
				}

				if (getResult.Status == "ready")
				{
					return new CaptchaResponse
					{
						CaptchaId = taskId.ToString(),
						Solution = getResult.Solution?.Text ?? ""
					};
				}

				if (sw.Elapsed.TotalSeconds > 120)
				{
					throw new TimeoutException($"[AntiCaptcha] Timed out after 120s waiting for taskId={taskId}");
				}
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
				Log.Information($"[AntiCaptcha] Could not find clientKey for taskId={taskId} in _taskKeyMapping.");
				return;
			}

			var payload = new
			{
				clientKey = usedKey,
				taskId = taskId
			};
			var json = JsonSerializer.Serialize(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");

			var resp = await _httpClient.PostAsync("https://api.anti-captcha.com/reportIncorrectImageCaptcha", content);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information($"[AntiCaptcha] ReportFailure => {respStr}");
		}

		// Clases que mapean EXACTAMENTE lo que AntiCaptcha envía
		private class CreateTaskResponse
		{
			[JsonPropertyName("errorId")]
			public int ErrorId { get; set; }

			[JsonPropertyName("errorCode")]
			public string ErrorCode { get; set; }

			[JsonPropertyName("errorDescription")]
			public string ErrorDescription { get; set; }

			[JsonPropertyName("taskId")]
			public int TaskId { get; set; }
		}

		private class GetTaskResultResponse
		{
			[JsonPropertyName("errorId")]
			public int ErrorId { get; set; }

			[JsonPropertyName("errorCode")]
			public string ErrorCode { get; set; }

			[JsonPropertyName("errorDescription")]
			public string ErrorDescription { get; set; }

			[JsonPropertyName("status")]
			public string Status { get; set; } // "ready" o "processing"

			[JsonPropertyName("solution")]
			public SolutionPart Solution { get; set; }
		}

		private class SolutionPart
		{
			[JsonPropertyName("text")]
			public string Text { get; set; }
		}
	}
}
