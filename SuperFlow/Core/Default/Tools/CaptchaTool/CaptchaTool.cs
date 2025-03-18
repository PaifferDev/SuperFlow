using Serilog;
using SuperFlow.Core.Contracts;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SuperFlow.Core.Default.Tools.CaptchaTool
{
	public class CaptchaTool : BaseTool
	{
		private readonly CaptchaToolConfig _config;
		private readonly ILogger? _logger;
		private static readonly ConcurrentDictionary<string, int> _failCounts = new(); // [providerName => failCount]
		private static bool _anySolutionReportedAsWrong = false;
		private const int TRUST_THRESHOLD = 8;

		public CaptchaTool(string name, CaptchaToolConfig config, IFlowLogger? flowLogger = null)
			: base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			if (_config.Providers.Count == 0)
				throw new InvalidOperationException("No hay proveedores de captcha registrados en CaptchaToolConfig.");
			// Si flowLogger no tiene GetSerilogLogger, intentamos castear
			_logger = flowLogger as ILogger;
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, object? parameters = null)
		{
			if (parameters is not CaptchaToolParameters captchaParams || captchaParams.ImageData.Length == 0)
				throw new ArgumentException("Se requiere 'ImageData' válido para resolver captcha.");

			for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
			{
				var (isSuccess, result) = await TrySolveOnce(context, captchaParams.ImageData, captchaParams.Sensitivity);
				if (isSuccess && result != null)
				{
					return new CaptchaToolResult
					{
						ProviderName = result.ProviderName,
						CaptchaText = result.CaptchaText,
						SolveTimeSeconds = result.SolveTimeSeconds,
						CaptchaId = result.CaptchaId
					};
				}
				else
				{
					_logger?.Error("[CaptchaTool] Falló la resolución del captcha en reintento #{Attempt}.", attempt + 1);
					await Task.Delay(1500);
				}
			}
			throw new InvalidOperationException("[CaptchaTool] No se pudo resolver el captcha tras los reintentos internos.");
		}

		private async Task<(bool isSuccess, CaptchaResult? result)> TrySolveOnce(FlowContext context, byte[] imageData, bool sensitivity)
		{
			var activeProviders = _config.Providers
				.Where(p => _failCounts.GetValueOrDefault(p.Name, 0) < 2)
				.ToList();

			if (!activeProviders.Any())
			{
				foreach (var key in _failCounts.Keys)
				{
					_failCounts[key] = 0;
				}
				activeProviders = _config.Providers.ToList();
			}

			try
			{
				var result = await SolveCaptchaWithPossibleConsensusAsync(activeProviders, imageData, _config.SolveTimeoutSeconds, sensitivity);
				return (true, result);
			}
			catch (Exception ex)
			{
				_logger?.Error(ex, "[CaptchaTool] Error en TrySolveOnce.");
				return (false, null);
			}
		}

		private async Task<CaptchaResult> SolveCaptchaWithPossibleConsensusAsync(
			List<ICaptchaProvider> providers,
			byte[] imageData,
			int solveTimeoutSeconds,
			bool sensitivity)
		{
			var tasks = providers
				.Select(provider => SolveWithProviderAsync(provider, imageData, solveTimeoutSeconds, sensitivity))
				.ToList();

			var successList = new List<(CaptchaResult result, TimeSpan elapsed)>();
			var solutionGroups = new Dictionary<string, List<(CaptchaResult, TimeSpan)>>();

			while (tasks.Any())
			{
				var finishedTask = await Task.WhenAny(tasks);
				tasks.Remove(finishedTask);

				var (isSuccess, result, elapsed) = await finishedTask;
				if (isSuccess && result != null)
				{
					successList.Add((result, elapsed));
					if (!solutionGroups.ContainsKey(result.CaptchaText))
						solutionGroups[result.CaptchaText] = new List<(CaptchaResult, TimeSpan)>();
					solutionGroups[result.CaptchaText].Add((result, elapsed));

					if (solutionGroups[result.CaptchaText].Count >= 2)
					{
						_logger?.Information("[CaptchaTool] Se alcanzó consenso con la solución '{Text}'.", result.CaptchaText);
						return result;
					}
					if (!_anySolutionReportedAsWrong)
					{
						var trustValue = providers.First(p => p.Name == result.ProviderName).Trust;
						if (trustValue >= TRUST_THRESHOLD)
						{
							_logger?.Information("[CaptchaTool] Respuesta aceptada inmediatamente de {Provider} (Trust={Trust}).", result.ProviderName, trustValue);
							return result;
						}
					}
				}
				else
				{
					if (result != null)
					{
						_failCounts.AddOrUpdate(result.ProviderName, 1, (_, old) => old + 1);
					}
				}
			}

			if (!successList.Any())
				throw new InvalidOperationException("[CaptchaTool] Ningún proveedor resolvió el captcha.");

			var best = successList
				.OrderBy(x => _failCounts.GetValueOrDefault(x.result.ProviderName, 0))
				.ThenByDescending(x => providers.First(p => p.Name == x.result.ProviderName).Trust)
				.ThenBy(x => x.elapsed)
				.First();

			_logger?.Information("[CaptchaTool] Fallback => {Provider} (Trust={Trust}), Tiempo={Time}s.", best.result.ProviderName,
				providers.First(p => p.Name == best.result.ProviderName).Trust, best.elapsed.TotalSeconds);
			return best.result;
		}

		private async Task<(bool isSuccess, CaptchaResult? result, TimeSpan elapsed)> SolveWithProviderAsync(
			ICaptchaProvider provider,
			byte[] imageData,
			int solveTimeoutSeconds,
			bool sensitivity)
		{
			var sw = Stopwatch.StartNew();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));
			try
			{
				var resp = await provider.SolveCaptchaAsync(imageData, cts.Token, sensitivity);
				sw.Stop();
				var captchaResult = new CaptchaResult(
					providerName: provider.Name,
					captchaText: resp.Solution,
					solveTimeSeconds: sw.Elapsed.TotalSeconds,
					captchaId: resp.CaptchaId
				);
				return (true, captchaResult, sw.Elapsed);
			}
			catch (Exception ex)
			{
				sw.Stop();
				_logger?.Error(ex, "[CaptchaTool] Error en SolveWithProviderAsync con {Provider}.", provider.Name);
				_failCounts.AddOrUpdate(provider.Name, 1, (_, old) => old + 1);
				return (false, null, sw.Elapsed);
			}
		}

		public async Task ReportFailureAsync(string providerName, string captchaId)
		{
			_anySolutionReportedAsWrong = true;
			var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
			if (provider != null)
			{
				await provider.ReportFailureAsync(captchaId);
				_failCounts.AddOrUpdate(providerName, 1, (_, old) => old + 1);
			}
		}
	}
}
