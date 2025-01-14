using SuperFlow.Core.Actions;
using SuperFlow.Core.Default.Actions.CaptchaAction.Models;
using SuperFlow.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SuperFlow.Core.Default.Actions.CaptchaAction
{
	/// <summary>
	/// Action que encapsula toda la lógica para resolver un captcha
	/// usando uno o varios ICaptchaProvider.
	/// </summary>
	public class CaptchaAction : BaseAction
	{
		private readonly CaptchaActionConfig _config;

		// Mapa [nombreProvider => cantidad de fallos]
		private readonly ConcurrentDictionary<string, int> _providerFailureCounts = new ConcurrentDictionary<string, int>();

		public CaptchaAction(string name, CaptchaActionConfig config) : base(name)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));

			if (_config.Providers.Count == 0)
				throw new InvalidOperationException("No hay proveedores de captcha registrados en CaptchaActionConfig.");
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, object? parameters = null)
		{
			var args = parameters as CaptchaActionParameters;
			if (args == null || args.ImageData.Length == 0)
			{
				throw new ArgumentException("Se requiere 'ImageData' válido para resolver captcha.");
			}

			// 1. Obtener la imagen del captcha en bytes
			byte[]? imageData = args?.ImageData;
			if (imageData == null || imageData.Length == 0)
			{
				throw new ArgumentException("No se recibió 'ImageData' para resolver el captcha.");
			}

			// 2. Intentar resolver el captcha (puedes manejar reintentos si gustas)
			var result = await SolveCaptchaWithRetriesAsync(imageData);

			// 3. Devolver algo: un objeto con la info del captcha resuelto
			return new
			{
				ProviderName = result.ProviderName,
				CaptchaText = result.CaptchaText,
				SolveTimeSeconds = result.SolveTimeSeconds,
				CaptchaId = result.CaptchaId
			};
		}

		/// <summary>
		/// Intenta resolver el captcha con reintentos (hasta _config.MaxRetries).
		/// Si falla con todos los providers en un intento, vuelve a pedir la imagen
		/// o lanza excepción (a tu elección).
		/// </summary>
		private async Task<CaptchaResult> SolveCaptchaWithRetriesAsync(byte[] imageData)
		{
			int attempt = 0;
			while (attempt < _config.MaxRetries)
			{
				attempt++;
				try
				{
					return await SolveCaptchaOnceAsync(imageData, _config.SolveTimeoutSeconds);
				}
				catch (Exception ex)
				{
					// Se vale loguear, re-lanzar, etc.
					Console.WriteLine($"[CaptchaAction] Falló intento #{attempt}: {ex.Message}");
				}
			}

			throw new InvalidOperationException($"No se pudo resolver captcha tras {_config.MaxRetries} reintentos.");
		}

		/// <summary>
		/// Realiza UN intento de resolver el captcha usando TODOS los providers en paralelo (o en orden),
		/// y retorna tan pronto uno lo resuelva satisfactoriamente.
		/// </summary>
		private async Task<CaptchaResult> SolveCaptchaOnceAsync(byte[] imageData, int solveTimeoutSeconds)
		{
			// 1. Ordenar providers por cantidad de fallos (asc). 
			//    (Opcional, si quieres dar prioridad a los que han fallado menos)
			var providers = _config.Providers
				.OrderBy(p => _providerFailureCounts.ContainsKey(p.Name) ? _providerFailureCounts[p.Name] : 0)
				.ToList();

			// 2. Crear tareas en paralelo
			var tasks = new List<Task<CaptchaResult>>();
			foreach (var provider in providers)
			{
				tasks.Add(SolveWithProvider(provider, imageData, solveTimeoutSeconds));
			}

			// 3. Esperar a que una tarea finalice (WhenAny).
			while (tasks.Count > 0)
			{
				Task<CaptchaResult> finishedTask = await Task.WhenAny(tasks);
				tasks.Remove(finishedTask);

				try
				{
					// Si una tarea terminó con éxito, devolvemos su result
					var result = await finishedTask;
					return result;
				}
				catch (TimeoutException tex)
				{
					// Un provider tardó demasiado => anotar fallo
					IncrementFailureFromMessage(tex.Message);
				}
				catch (Exception ex)
				{
					// Cualquier otro error => anotar fallo
					IncrementFailureFromMessage(ex.Message);
				}
			}

			// Si llegamos aquí, TODOS los providers fallaron en este intento
			throw new InvalidOperationException("Todos los proveedores fallaron al resolver el captcha.");
		}

		/// <summary>
		/// Llama a un provider con un CancellationToken de 'solveTimeoutSeconds'.
		/// Similar a lo que hacía SolveWithProvider(...) en tu CaptchaSolverService.
		/// </summary>
		private async Task<CaptchaResult> SolveWithProvider(ICaptchaProvider provider, byte[] imageData, int solveTimeoutSeconds)
		{
			var stopwatch = Stopwatch.StartNew();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));

			try
			{
				var providerResponse = await provider.SolveCaptchaAsync(imageData, cts.Token);
				stopwatch.Stop();

				// providerResponse => (CaptchaId, Solution)
				return new CaptchaResult(
					providerName: provider.Name,
					captchaText: providerResponse.Solution,
					solveTimeSeconds: stopwatch.Elapsed.TotalSeconds,
					captchaId: providerResponse.CaptchaId
				);
			}
			catch (OperationCanceledException)
			{
				stopwatch.Stop();
				throw new TimeoutException($"[CaptchaAction] Se agotaron {solveTimeoutSeconds}s con el provider {provider.Name}");
			}
			catch
			{
				stopwatch.Stop();
				throw; // Se manejará en SolveCaptchaOnceAsync
			}
		}

		/// <summary>
		/// Analiza el mensaje de excepción en busca del provider y 
		/// aumenta el contador de fallos para él.
		/// </summary>
		private void IncrementFailureFromMessage(string message)
		{
			var prefix = "provider ";
			// Ejemplo: "[CaptchaAction] Se agotaron 70s con el provider TwoCaptcha"
			// Ajusta tu parsing según tus logs
			int index = message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
			if (index >= 0)
			{
				string providerName = message.Substring(index + prefix.Length).Trim();
				_providerFailureCounts.AddOrUpdate(providerName, 1, (k, oldV) => oldV + 1);
			}
		}

		// ---------------------------------------------------------------------------------------
		// Opcional: un método para reportar captchas fallidos 
		//           (si la librería ICaptchaProvider lo contempla).
		// ---------------------------------------------------------------------------------------

		public async Task ReportFailureAsync(string providerName, string captchaId)
		{
			var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
			if (provider != null)
			{
				await provider.ReportFailureAsync(captchaId);
				_providerFailureCounts.AddOrUpdate(providerName, 1, (k, oldV) => oldV + 1);
			}
		}
	}
}
