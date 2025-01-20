using SuperFlow.Core.Tools;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using SuperFlow.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SuperFlow.Core.Default.Tools.CaptchaTool
{
	public class CaptchaTool : BaseTool
	{
		private readonly CaptchaToolConfig _config;

		// Contador de fallas global [providerName => failsCount]
		private static readonly ConcurrentDictionary<string, int> _failCounts = new ConcurrentDictionary<string, int>();

		public CaptchaTool(string name, CaptchaToolConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			if (_config.Providers.Count == 0)
				throw new InvalidOperationException("No hay proveedores de captcha registrados en CaptchaToolConfig.");
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, object? parameters = null)
		{
			var args = parameters as CaptchaToolParameters;
			if (args == null || args.ImageData.Length == 0)
				throw new ArgumentException("Se requiere 'ImageData' válido para resolver captcha.");

			// Filtrar proveedores activos
			var activeProviders = _config.Providers
				.Where(p => _failCounts.GetValueOrDefault(p.Name, 0) < 2)
				.ToList();

			// Reiniciar contadores si todos los proveedores están excluidos
			if (!activeProviders.Any())
			{
				foreach (var kvp in _failCounts.Keys)
				{
					_failCounts[kvp] = 0;
				}
				activeProviders = _config.Providers.ToList();
			}

			// Resolver captcha
			var result = await SolveCaptchaAsync(activeProviders, args.ImageData, _config.SolveTimeoutSeconds);
			return new
			{
				ProviderName = result.ProviderName,
				CaptchaText = result.CaptchaText,
				SolveTimeSeconds = result.SolveTimeSeconds,
				CaptchaId = result.CaptchaId
			};
		}

		private async Task<CaptchaResult> SolveCaptchaAsync(
			List<ICaptchaProvider> providers,
			byte[] imageData,
			int solveTimeoutSeconds)
		{
			var tasks = providers.ToDictionary(
				provider => SolveWithProviderAsync(provider, imageData, solveTimeoutSeconds),
				provider => provider.Name);

			var solutionGroups = new Dictionary<string, List<(CaptchaResult result, TimeSpan elapsed)>>();
			var successList = new List<(CaptchaResult result, TimeSpan elapsed)>();

			while (tasks.Count > 0)
			{
				var finishedTask = await Task.WhenAny(tasks.Keys);
				var providerName = tasks[finishedTask];
				tasks.Remove(finishedTask);

				var (isSuccess, result, elapsed) = await finishedTask;

				if (isSuccess)
				{
					var cr = result!;
					successList.Add((cr, elapsed));

					if (!solutionGroups.ContainsKey(cr.CaptchaText))
					{
						solutionGroups[cr.CaptchaText] = new List<(CaptchaResult, TimeSpan)>();
					}
					solutionGroups[cr.CaptchaText].Add((cr, elapsed));

					if (solutionGroups[cr.CaptchaText].Count >= 2)
					{
						return cr;
					}
				}
				else
				{
					_failCounts.AddOrUpdate(providerName, 1, (_, old) => old + 1);
				}
			}

			if (successList.Count == 0)
			{
				throw new InvalidOperationException("No se pudo resolver el captcha.");
			}

			return successList
				.OrderBy(x => _failCounts.GetValueOrDefault(x.result.ProviderName, 0))
				.ThenBy(x => x.elapsed)
				.First().result;
		}

		private async Task<(bool isSuccess, CaptchaResult? result, TimeSpan elapsed)> SolveWithProviderAsync(
			ICaptchaProvider provider,
			byte[] imageData,
			int solveTimeoutSeconds)
		{
			var stopwatch = Stopwatch.StartNew();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));

			try
			{
				var response = await provider.SolveCaptchaAsync(imageData, cts.Token);
				stopwatch.Stop();

				return (true, new CaptchaResult(
					providerName: provider.Name,
					captchaText: response.Solution,
					solveTimeSeconds: stopwatch.Elapsed.TotalSeconds,
					captchaId: response.CaptchaId
				), stopwatch.Elapsed);
			}
			catch
			{
				stopwatch.Stop();
				return (false, null, stopwatch.Elapsed);
			}
		}

		public async Task ReportFailureAsync(string providerName, string captchaId)
		{
			var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
			if (provider != null)
			{
				await provider.ReportFailureAsync(captchaId);
				_failCounts.AddOrUpdate(providerName, 1, (_, old) => old + 1);
			}
		}
	}
}
