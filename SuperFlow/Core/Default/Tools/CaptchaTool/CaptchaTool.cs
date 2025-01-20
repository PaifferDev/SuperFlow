using SuperFlow.Core.Tools;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using SuperFlow.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SuperFlow.Core.Default.Tools.CaptchaTool
{
	/// <summary>
	/// CaptchaTool que:
	/// - Corre múltiples proveedores en paralelo.
	/// - Retorna inmediatamente si >=2 coinciden en la misma respuesta.
	/// - Si no hay coincidencia, usa la "mejor" respuesta (menos fallas, luego más rápido).
	/// - Cada proveedor con >=2 fallas se excluye en la siguiente ejecución.
	/// - Si todos quedan excluidos, se resetea.
	/// - Se maneja MaxRetries (reintento global).
	/// </summary>
	public class CaptchaTool : BaseTool
	{
		private readonly CaptchaToolConfig _config;

		// Contador de fallas global [providerName => failsCount].
		// Si failsCount >= 2 => excluimos en la siguiente ronda.
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

			byte[] imageData = args.ImageData;
			if (imageData == null || imageData.Length == 0)
				throw new ArgumentException("No se recibió 'ImageData' para resolver el captcha.");

			int attempt = 0;
			while (attempt < _config.MaxRetries)
			{
				attempt++;
				try
				{
					// 1. Resolver en UNA ronda
					var result = await SolveOneRoundAsync(imageData, _config.SolveTimeoutSeconds);
					// 2. Devolver en objeto anónimo
					return new
					{
						ProviderName = result.ProviderName,
						CaptchaText = result.CaptchaText,
						SolveTimeSeconds = result.SolveTimeSeconds,
						CaptchaId = result.CaptchaId
					};
				}
				catch (Exception ex)
				{
					// Si la ronda entera falla => reintento
					Debug.WriteLine($"[CaptchaTool] Falló la ronda #{attempt} => {ex.Message}");
				}
			}

			throw new InvalidOperationException($"No se pudo resolver captcha tras {_config.MaxRetries} reintentos.");
		}

		/// <summary>
		/// Corre 1 ronda: 
		/// - filtra proveedores (excluye con fails>=2), 
		/// - si todos excluidos => reset a 0,
		/// - lanza tareas => WhenAny => en cuanto 2 coincidan => return,
		/// - si se acaban => fallback a la "mejor" (menos fails, luego más rápida),
		/// - si ni una exitosa => lanza excepción.
		/// </summary>
		private async Task<CaptchaResult> SolveOneRoundAsync(byte[] imageData, int solveTimeoutSeconds)
		{
			// A) Filtrar con fails < 2
			var activeProviders = _config.Providers
				.Where(p => _failCounts.GetValueOrDefault(p.Name, 0) < 2)
				.ToList();

			if (!activeProviders.Any())
			{
				// Reset
				foreach (var kvp in _failCounts)
					_failCounts[kvp.Key] = 0;

				// Todos se "des-excluyen"
				activeProviders = _config.Providers.ToList();
			}

			// B) Crear una tarea por provider
			//    Cada tarea => (exito => CaptchaResult, fails => null)
			var tasks = new Dictionary<Task<(bool isSuccess, CaptchaResult? result, Exception? ex, TimeSpan? elapsed)>, string>();
			foreach (var provider in activeProviders)
			{
				var t = SolveWithProviderAsync(provider, imageData, solveTimeoutSeconds);
				tasks[t] = provider.Name;
			}

			// Se guardan los result en un diccionario => [ providerName => CaptchaResult con su tiempo...]
			var successList = new List<(CaptchaResult cr, TimeSpan elapsed)>();
			// Clave => captchaText => #de coincidencias => lista
			var solutionGroups = new Dictionary<string, List<(CaptchaResult cr, TimeSpan elapsed)>>();

			// C) Recibir las tareas de a una => WhenAny => chequear coincidencias
			while (tasks.Count > 0)
			{
				var finishedTask = await Task.WhenAny(tasks.Keys);
				var providerName = tasks[finishedTask];
				tasks.Remove(finishedTask);

				var (isSuccess, result, ex, elapsed) = finishedTask.Result;

				if (!isSuccess)
				{
					// Falla => incrementa su fail
					_failCounts.AddOrUpdate(providerName, 1, (_, oldv) => oldv + 1);
					Debug.WriteLine($"[CaptchaTool] Provider {providerName} fail => {ex?.Message}");
				}
				else
				{
					// Éxito
					var cr = result!;
					var e = elapsed!.Value;
					successList.Add((cr, e));
					// Sumar a solutionGroups
					if (!solutionGroups.ContainsKey(cr.CaptchaText))
						solutionGroups[cr.CaptchaText] = new List<(CaptchaResult, TimeSpan)>();
					solutionGroups[cr.CaptchaText].Add((cr, e));

					// Verificar si ya tenemos al menos 2 con esa solution
					if (solutionGroups[cr.CaptchaText].Count >= 2)
					{
						// Retornamos inmediatamente
						Debug.WriteLine($"[CaptchaTool] Se obtuvo coincidencia de 2 providers => {cr.CaptchaText}");
						return cr;
						// (Podrías devolver la *más rápida* de ese group, 
						//  pero si se van sumando en orden de finalización, da igual.)
					}
				}
			}

			// D) Si llegamos aquí => no hubo coincidencia >=2
			// => fallback
			if (successList.Count == 0)
				throw new InvalidOperationException("Ningún provider devolvió resultado exitoso.");

			// => Tomar la "mejor" => 
			//  criterio = "menos fails" => si tie => "más rápido"
			//  => necesitamos saber fails y su elapsed
			// successList => (CaptchaResult cr, TimeSpan elapsed)
			// recordemos cr.ProviderName

			// 1) Agrupa por provider, saca su failCount
			// 2) Ordena: failCount asc, then elapsed asc
			var best = successList
				.OrderBy(x => _failCounts.GetValueOrDefault(x.cr.ProviderName, 0))
				.ThenBy(x => x.elapsed)
				.First();

			Debug.WriteLine($"[CaptchaTool] No hubo coincidencia => se escoge la 'mejor': {best.cr.CaptchaText} de {best.cr.ProviderName}");
			return best.cr;
		}

		/// <summary>
		/// Invoca a un provider con un CancellationToken de solveTimeoutSeconds. 
		/// Retorna (isSuccess, CaptchaResult?, Exception?, TimeSpan?).
		/// isSuccess => true si no lanzó excepción.
		/// </summary>
		private async Task<(bool isSuccess, CaptchaResult? result, Exception? ex, TimeSpan? elapsed)> SolveWithProviderAsync(
			ICaptchaProvider provider,
			byte[] imageData,
			int solveTimeoutSeconds)
		{
			var stopwatch = Stopwatch.StartNew();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));

			try
			{
				var resp = await provider.SolveCaptchaAsync(imageData, cts.Token);
				stopwatch.Stop();
				// ok
				var cr = new CaptchaResult(
					providerName: provider.Name,
					captchaText: resp.Solution,
					solveTimeSeconds: stopwatch.Elapsed.TotalSeconds,
					captchaId: resp.CaptchaId
				);
				return (true, cr, null, stopwatch.Elapsed);
			}
			catch (OperationCanceledException exCancel)
			{
				stopwatch.Stop();
				return (false, null, new TimeoutException($"Timeout {solveTimeoutSeconds}s en {provider.Name}", exCancel), stopwatch.Elapsed);
			}
			catch (Exception ex)
			{
				stopwatch.Stop();
				return (false, null, ex, stopwatch.Elapsed);
			}
		}

		public async Task ReportFailureAsync(string providerName, string captchaId)
		{
			var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
			if (provider != null)
			{
				await provider.ReportFailureAsync(captchaId);
				// Ajustar contadores => si deseas que "reportFailure" incremente la estadística 
				// de fallas, añade algo como:
				// _failCounts.AddOrUpdate(providerName, 1, (_, oldV) => oldV + 1);
			}
		}
	}
}
