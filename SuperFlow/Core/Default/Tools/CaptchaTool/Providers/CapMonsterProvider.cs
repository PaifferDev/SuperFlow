using Serilog;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperFlow.Core.Default.Tools.CaptchaTool.Providers
{
	public class CapMonsterProvider : ICaptchaProvider
	{
		private readonly HttpClient _httpClient;
		private readonly string _apiKey;
		private readonly int _pollingDelayMs;
		private readonly Random _random;
		private readonly ConcurrentDictionary<int, string> _taskKeyMapping;

		public string Name => "CapMonster";
		public int Trust => 5;
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
				throw new ArgumentException("Empty captcha image data.");

			string base64Image = Convert.ToBase64String(imageData);
			var createPayload = new { clientKey = _apiKey, task = new { type = "ImageToTextTask", body = base64Image } };
			var createJson = JsonSerializer.Serialize(createPayload);
			using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

			HttpResponseMessage createResp;
			try
			{
				createResp = await _httpClient.PostAsync("https://api.capmonster.cloud/createTask", createContent, cancelToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[CapMonster] Error al enviar createTask.");
				throw;
			}
			var createStr = await createResp.Content.ReadAsStringAsync(cancelToken);
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var createResult = JsonSerializer.Deserialize<CreateTaskResponse>(createStr, options);
			if (createResult == null)
				throw new Exception($"[CapMonster] Error al parsear createTask: {createStr}");
			if (createResult.ErrorId != 0)
				throw new Exception($"[CapMonster] createTask Error: {createResult.ErrorCode} - {createResult.ErrorDescription}");
			if (createResult.TaskId == 0)
				throw new Exception($"[CapMonster] createTask devolvió TaskId=0: {createStr}");

			int taskId = createResult.TaskId;
			_taskKeyMapping[taskId] = _apiKey;
			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);
				var getPayload = new { clientKey = _apiKey, taskId = taskId };
				var getJson = JsonSerializer.Serialize(getPayload);
				using var getContent = new StringContent(getJson, Encoding.UTF8, "application/json");
				HttpResponseMessage getResp;
				try
				{
					getResp = await _httpClient.PostAsync("https://api.capmonster.cloud/getTaskResult", getContent, cancelToken);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "[CapMonster] Error en getTaskResult.");
					throw;
				}
				var getStr = await getResp.Content.ReadAsStringAsync(cancelToken);
				var getResult = JsonSerializer.Deserialize<GetTaskResultResponse>(getStr, options);
				if (getResult == null)
					throw new Exception($"[CapMonster] No se pudo parsear getTaskResult: {getStr}");
				if (getResult.ErrorId != 0)
					throw new Exception($"[CapMonster] getTaskResult Error: {getResult.ErrorCode} - {getResult.ErrorDescription}");
				if (getResult.Status == "ready")
				{
					Log.Information("[CapMonster] Captcha resuelto para TaskId={TaskId}.", taskId);
					return new CaptchaResponse { CaptchaId = taskId.ToString(), Solution = getResult.Solution?.Text ?? "" };
				}
				if (sw.Elapsed.TotalSeconds > 120)
					throw new TimeoutException($"[CapMonster] Timeout tras 120s esperando la solución (TaskId={taskId}).");
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
				Log.Information($"[CapMonster] No se encontró API key para TaskId={taskId}.");
				return;
			}
			var payload = new { clientKey = usedKey, taskId = taskId };
			var json = JsonSerializer.Serialize(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			var reportUrl = "https://api.capmonster.cloud/reportIncorrectImageCaptcha";
			var resp = await _httpClient.PostAsync(reportUrl, content);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information("[CapMonster] ReportFailure: {Response}", respStr);
		}

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
