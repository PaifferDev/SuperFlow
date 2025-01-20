using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class CapMonsterProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly string _apiKey; // clientKey
		private readonly int _pollingDelayMs;
		private readonly Random _random;
		private readonly ConcurrentDictionary<int, string> _taskKeyMapping;

		public string Name => "CapMonster";
		public double? AverageSolveTimeSeconds => null;
		public decimal? CostPerCaptcha => null;

		public CapMonsterProvider(HttpClient httpClient, string apiKey, int pollingDelayMs = 5000)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
			if (string.IsNullOrEmpty(_apiKey))
				throw new ArgumentException("Debes proporcionar un API Key para CapMonster.");

			_pollingDelayMs = pollingDelayMs;
			_random = new Random();
			_taskKeyMapping = new ConcurrentDictionary<int, string>();
		}

		public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken cancelToken = default)
		{
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("La imagen del captcha está vacía.", nameof(imageData));

			// 1) Crear la tarea (ImageToTextTask)
			string base64Image = Convert.ToBase64String(imageData);
			var createPayload = new
			{
				clientKey = _apiKey,
				task = new
				{
					type = "ImageToTextTask",
					body = base64Image
				}
			};

			var createJson = JsonSerializer.Serialize(createPayload);
			using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

			var createResp = await _httpClient.PostAsync("https://api.capmonster.cloud/createTask", createContent, cancelToken);
			var createStr = await createResp.Content.ReadAsStringAsync(cancelToken);

			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var createResult = JsonSerializer.Deserialize<CreateTaskResponse>(createStr, options);

			if (createResult == null)
			{
				throw new Exception($"[CapMonster] Error al parsear createTask: {createStr}");
			}
			if (createResult.ErrorId != 0)
			{
				throw new Exception($"[CapMonster] createTask Error: {createResult.ErrorCode} - {createResult.ErrorDescription}");
			}
			if (createResult.TaskId == 0)
			{
				throw new Exception($"[CapMonster] createTask devolvió taskId=0 => {createStr}");
			}

			int taskId = createResult.TaskId;
			_taskKeyMapping[taskId] = _apiKey; // Por si luego quieres reportar fallo

			// 2) getTaskResult
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);

				var getPayload = new
				{
					clientKey = _apiKey,
					taskId = taskId
				};
				var getJson = JsonSerializer.Serialize(getPayload);
				using var getContent = new StringContent(getJson, Encoding.UTF8, "application/json");

				var getResp = await _httpClient.PostAsync("https://api.capmonster.cloud/getTaskResult", getContent, cancelToken);
				var getStr = await getResp.Content.ReadAsStringAsync(cancelToken);

				var getResult = JsonSerializer.Deserialize<GetTaskResultResponse>(getStr, options);
				if (getResult == null)
				{
					throw new Exception($"[CapMonster] Error al parsear getTaskResult => {getStr}");
				}
				if (getResult.ErrorId != 0)
				{
					throw new Exception($"[CapMonster] getTaskResult Error: {getResult.ErrorCode} - {getResult.ErrorDescription}");
				}
				if (getResult.Status == "ready")
				{
					return new CaptchaResponse
					{
						CaptchaId = taskId.ToString(),
						Solution = getResult.Solution?.Text ?? ""
					};
				}

				if (stopwatch.Elapsed.TotalSeconds > 120)
				{
					throw new TimeoutException($"[CapMonster] Se superaron 120s esperando la solución del captcha (taskId={taskId}).");
				}
			}
		}

		public async Task ReportFailureAsync(string captchaId)
		{
			// CapMonster (similar a AntiCaptcha) permite reportar "reportIncorrectImageCaptcha"
			// con clientKey y taskId. Si no lo requieres, puedes dejarlo "vacío".
			if (string.IsNullOrEmpty(captchaId))
				return;
			if (!int.TryParse(captchaId, out int taskId))
				return;

			if (!_taskKeyMapping.TryGetValue(taskId, out var usedKey))
			{
				Log.Information($"[CapMonster] No se encontró la key para el taskId={taskId} en _taskKeyMapping.");
				return;
			}

			// doc: https://capmonster.cloud/apidoc/reportIncorrectImageCaptcha
			// (En el momento de escribir esto, la doc es muy parecida a AntiCaptcha.)
			var payload = new
			{
				clientKey = usedKey,
				taskId = taskId
			};
			var json = JsonSerializer.Serialize(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");

			// En CapMonster, la URL es:
			// "https://api.capmonster.cloud/reportIncorrectRecaptcha" u "reportIncorrectImageCaptcha"
			// Dependiendo de si es recaptcha o imagen. Para "ImageToText", usar:
			var reportUrl = "https://api.capmonster.cloud/reportIncorrectImageCaptcha";

			var resp = await _httpClient.PostAsync(reportUrl, content);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information($"[CapMonster] ReportFailure => {respStr}");
		}

		// Clases auxiliares
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
			public string Status { get; set; }

			[JsonPropertyName("solution")]
			public SolutionInfo Solution { get; set; }
		}

		private class SolutionInfo
		{
			[JsonPropertyName("text")]
			public string Text { get; set; }
		}
	}
}
