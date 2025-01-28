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
		private readonly IFlowLogger? _logger;

		// Contador de fallas global [providerName => failsCount]
		private static readonly ConcurrentDictionary<string, int> _failCounts = new();

		// Flag estático que se vuelve true cuando al menos un captcha se reportó como mal resuelto
		// y, desde entonces, se exigen 2 proveedores coincidentes para aceptar la solución.
		private static bool _anySolutionReportedAsWrong = false;

		// Umbral de Trust para aceptar de inmediato SI no se ha reportado ningún error antes
		private const int TRUST_THRESHOLD = 8;

		public CaptchaTool(string name, CaptchaToolConfig config, IFlowLogger? flowLogger)
			: base(name)
		{
			if (flowLogger != null)
			{
				_logger = flowLogger;
			}

			_config = config ?? throw new ArgumentNullException(nameof(config));
			if (_config.Providers.Count == 0)
				throw new InvalidOperationException("No hay proveedores de captcha registrados en CaptchaToolConfig.");
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, object? parameters = null)
		{
			var args = parameters as CaptchaToolParameters;
			if (args == null || args.ImageData.Length == 0)
				throw new ArgumentException("Se requiere 'ImageData' válido para resolver captcha.");

			// 1) Filtrar proveedores activos (menos de 2 fallas consecutivas)
			var activeProviders = _config.Providers
				.Where(p => _failCounts.GetValueOrDefault(p.Name, 0) < 2)
				.ToList();

			// Si todos están con >=2 fallas, reseteamos contadores y volvemos a incluirlos
			if (!activeProviders.Any())
			{
				foreach (var key in _failCounts.Keys)
				{
					_failCounts[key] = 0;
				}
				activeProviders = _config.Providers.ToList();
			}

			// 2) Resolver
			var result = await SolveCaptchaWithPossibleConsensusAsync(
				activeProviders,
				args.ImageData,
				_config.SolveTimeoutSeconds);

			return new
			{
				ProviderName = result.ProviderName,
				CaptchaText = result.CaptchaText,
				SolveTimeSeconds = result.SolveTimeSeconds,
				CaptchaId = result.CaptchaId
			};
		}

		/// <summary>
		/// Lógica que lanza todos los proveedores en paralelo.
		/// Si NO se ha reportado ningún error globalmente, 
		/// podemos aceptar en caliente la primera respuesta de un proveedor con Trust alto.
		/// Si YA se ha reportado un error, forzamos a que haya 2 respuestas iguales para aceptar.
		/// </summary>
		private async Task<CaptchaResult> SolveCaptchaWithPossibleConsensusAsync(
			List<ICaptchaProvider> providers,
			byte[] imageData,
			int solveTimeoutSeconds)
		{
			var tasks = providers.ToDictionary(
				provider => SolveWithProviderAsync(provider, imageData, solveTimeoutSeconds),
				provider => provider.Name
			);

			// Para almacenar éxitos
			var successList = new List<(CaptchaResult result, TimeSpan elapsed)>();
			// Agrupar por texto => ver cuántos coinciden
			var solutionGroups = new Dictionary<string, List<(CaptchaResult, TimeSpan)>>();

			// Mientras queden tareas
			while (tasks.Count > 0)
			{
				var finishedTask = await Task.WhenAny(tasks.Keys);
				var providerName = tasks[finishedTask];
				tasks.Remove(finishedTask);

				var (isSuccess, result, elapsed) = await finishedTask;
				if (isSuccess && result != null)
				{
					successList.Add((result, elapsed));

					if (!solutionGroups.ContainsKey(result.CaptchaText))
						solutionGroups[result.CaptchaText] = new List<(CaptchaResult, TimeSpan)>();
					solutionGroups[result.CaptchaText].Add((result, elapsed));

					// Chequear consenso => 2 con la misma respuesta
					if (solutionGroups[result.CaptchaText].Count >= 2)
					{
						_logger?.LogError($"[CaptchaTool] Se alcanzó consenso con texto '{result.CaptchaText}'. Retornando.");
						return result;
					}

					// Si aún NO se ha reportado ningún error global (anySolutionReportedAsWrong==false),
					// podemos aceptar la primera respuesta de alto Trust
					if (!_anySolutionReportedAsWrong)
					{
						var trustValue = providers.First(p => p.Name == result.ProviderName).Trust;
						if (trustValue >= TRUST_THRESHOLD)
						{
							_logger?.LogError($"[CaptchaTool] Respuesta aceptada inmediatamente de {result.ProviderName} (Trust={trustValue}).");
							return result;
						}
					}
					// Nota: si _anySolutionReportedAsWrong == true, no aceptamos en caliente.
				}
				else
				{
					// Fallo => incrementar fallCount
					_failCounts.AddOrUpdate(providerName, 1, (_, old) => old + 1);
				}
			}

			// Si no hay éxitos, error
			if (successList.Count == 0)
				throw new InvalidOperationException("No se pudo resolver el captcha (ningún proveedor tuvo éxito).");

			// No se logró consenso ni se aceptó en caliente => tomamos la 'mejor' (fallback)
			var best = successList
				.OrderBy(x => _failCounts.GetValueOrDefault(x.result.ProviderName, 0))
				.ThenByDescending(x => providers.First(p => p.Name == x.result.ProviderName).Trust)
				.ThenBy(x => x.elapsed)
				.First();

			_logger?.LogError($"[CaptchaTool] Fallback => {best.result.ProviderName} (Trust={providers.First(p => p.Name == best.result.ProviderName).Trust}), Tiempo={best.elapsed.TotalSeconds}");
			return best.result;
		}

		/// <summary>
		/// Llama al proveedor individual con timeout y maneja errores.
		/// </summary>
		private async Task<(bool isSuccess, CaptchaResult? result, TimeSpan elapsed)>
			SolveWithProviderAsync(ICaptchaProvider provider, byte[] imageData, int solveTimeoutSeconds)
		{
			var sw = Stopwatch.StartNew();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));
			try
			{
				var resp = await provider.SolveCaptchaAsync(imageData, cts.Token);
				sw.Stop();
				var cr = new CaptchaResult(
					providerName: provider.Name,
					captchaText: resp.Solution,
					solveTimeSeconds: sw.Elapsed.TotalSeconds,
					captchaId: resp.CaptchaId
				);
				return (true, cr, sw.Elapsed);
			}
			catch (Exception ex)
			{
				sw.Stop();
				_logger?.LogError($"[CaptchaTool] Error en SolveWithProviderAsync '{provider.Name}'", ex);
				return (false, null, sw.Elapsed);
			}
		}

		/// <summary>
		/// Se llama cuando externamente detectaron que la solución fue errónea.
		/// Marcamos la variable estática para forzar esperar 2 resultados en siguientes llamadas.
		/// </summary>
		public async Task ReportFailureAsync(string providerName, string captchaId)
		{
			// Marcar el "error global"
			_anySolutionReportedAsWrong = true;

			// Además, incrementamos el failCount del proveedor
			var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
			if (provider != null)
			{
				await provider.ReportFailureAsync(captchaId);
				_failCounts.AddOrUpdate(providerName, 1, (_, old) => old + 1);
			}
		}
	}
}
