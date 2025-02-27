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
			var base64Img = Convert.ToBase64String(imageData);
			var createPayload = new { clientKey = selectedKey, task = new { type = "ImageToTextTask", body = base64Img } };
			var createJson = JsonSerializer.Serialize(createPayload);
			using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

			HttpResponseMessage createResponse;
			try
			{
				createResponse = await _httpClient.PostAsync("https://api.anti-captcha.com/createTask", createContent, cancelToken);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[AntiCaptcha] Error al enviar createTask.");
				throw;
			}
			var createString = await createResponse.Content.ReadAsStringAsync(cancelToken);
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var createResult = JsonSerializer.Deserialize<CreateTaskResponse>(createString, options);
			if (createResult == null)
				throw new Exception($"[AntiCaptcha] No se pudo parsear createTask: {createString}");
			if (createResult.ErrorId != 0)
				throw new Exception($"[AntiCaptcha] createTask Error: {createResult.ErrorCode} - {createResult.ErrorDescription}");
			if (createResult.TaskId == 0)
				throw new Exception($"[AntiCaptcha] createTask devolvió TaskId=0: {createString}");

			int taskId = createResult.TaskId;
			_taskKeyMapping[taskId] = selectedKey;
			var sw = Stopwatch.StartNew();
			while (true)
			{
				cancelToken.ThrowIfCancellationRequested();
				await Task.Delay(_pollingDelayMs, cancelToken);

				var getPayload = new { clientKey = selectedKey, taskId = taskId };
				var getJson = JsonSerializer.Serialize(getPayload);
				using var getContent = new StringContent(getJson, Encoding.UTF8, "application/json");
				HttpResponseMessage getResp;
				try
				{
					getResp = await _httpClient.PostAsync("https://api.anti-captcha.com/getTaskResult", getContent, cancelToken);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "[AntiCaptcha] Error en getTaskResult.");
					throw;
				}
				var getStringResp = await getResp.Content.ReadAsStringAsync(cancelToken);
				var getResult = JsonSerializer.Deserialize<GetTaskResultResponse>(getStringResp, options);
				if (getResult == null)
					throw new Exception($"[AntiCaptcha] No se pudo parsear getTaskResult: {getStringResp}");
				if (getResult.ErrorId != 0)
					throw new Exception($"[AntiCaptcha] getTaskResult Error: {getResult.ErrorCode} - {getResult.ErrorDescription}");
				if (getResult.Status == "ready")
				{
					Log.Information("[AntiCaptcha] Captcha resuelto para TaskId={TaskId}.", taskId);
					return new CaptchaResponse { CaptchaId = taskId.ToString(), Solution = getResult.Solution?.Text ?? "" };
				}
				if (sw.Elapsed.TotalSeconds > 120)
					throw new TimeoutException($"[AntiCaptcha] Timeout después de 120s esperando el captcha (TaskId={taskId}).");
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
				Log.Information($"[AntiCaptcha] No se encontró clientKey para TaskId={taskId}.");
				return;
			}
			var payload = new { clientKey = usedKey, taskId = taskId };
			var json = JsonSerializer.Serialize(payload);
			using var content = new StringContent(json, Encoding.UTF8, "application/json");
			var resp = await _httpClient.PostAsync("https://api.anti-captcha.com/reportIncorrectImageCaptcha", content);
			var respStr = await resp.Content.ReadAsStringAsync();
			Log.Information("[AntiCaptcha] ReportFailure: {Response}", respStr);
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
			public SolutionPart Solution { get; set; }
		}

		private class SolutionPart
		{
			[JsonPropertyName("text")]
			public string Text { get; set; }
		}
	}
}
