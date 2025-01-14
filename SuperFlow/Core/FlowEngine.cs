using SuperFlow.Core.Contracts;
using SuperFlow.Core.Models;

namespace SuperFlow.Core
{
    /// <summary>
    /// Motor principal para orquestar Steps y manejar transiciones.
    /// </summary>
    public class FlowEngine
	{
		private readonly Dictionary<string, IStep> _steps = new();
		private readonly Dictionary<(string fromStep, string resultCode), NextStepInfo> _transitions = new();
		private string? _initialStep;

		private readonly IFlowLogger? _logger;

		public FlowEngine(IFlowLogger? logger = null)
		{
			_logger = logger;
		}

		public void RegisterStep(IStep step, bool isInitial = false)
		{
			if (step == null) throw new ArgumentNullException(nameof(step));
			_steps[step.Name] = step;

			if (isInitial)
				_initialStep = step.Name;
		}

		/// <summary>
		/// Transición normal (Single): de StepOrigen + resultCode => StepDestino
		/// </summary>
		public void SetTransition(string fromStep, string resultCode, string toStep)
		{
			_transitions[(fromStep, resultCode)] = new NextStepInfo
			{
				TransitionType = TransitionType.Single,
				StepNames = new List<string> { toStep }
			};
		}

		/// <summary>
		/// Transición paralela (Parallel): de StepOrigen + resultCode => varios Steps en paralelo
		/// </summary>
		public void SetParallelTransition(string fromStep, string resultCode, params string[] parallelSteps)
		{
			_transitions[(fromStep, resultCode)] = new NextStepInfo
			{
				TransitionType = TransitionType.Parallel,
				StepNames = parallelSteps.ToList()
			};
		}

		public async Task RunAsync(FlowContext context)
		{
			if (_initialStep == null)
				throw new InvalidOperationException("No se ha definido un Step inicial.");

			_logger?.LogFlowStart();
			var currentStepName = _initialStep;
			bool finished = false;

			while (!finished)
			{
				if (!_steps.ContainsKey(currentStepName))
					throw new Exception($"El step '{currentStepName}' no está registrado.");

				var step = _steps[currentStepName];

				_logger?.LogStepStart(step.Name);

				StepResult result;
				try
				{
					result = await step.ExecuteAsync(context);
				}
				catch (Exception ex)
				{
					_logger?.LogError($"Error en Step '{step.Name}'", ex);
					throw;
				}

				_logger?.LogStepEnd(step.Name, result);

				// Buscar transición
				if (_transitions.TryGetValue((step.Name, result.ResultCode), out var nextStepInfo))
				{
					switch (nextStepInfo.TransitionType)
					{
						case TransitionType.Single:
							currentStepName = nextStepInfo.StepNames.First();
							break;

						case TransitionType.Parallel:
							var parallelDestinations = nextStepInfo.StepNames;
							_logger?.LogParallelStepStart(parallelDestinations);

							var parallelResults = await ExecuteParallelSteps(context, parallelDestinations);
							_logger?.LogParallelStepEnd(parallelResults);

							bool allSuccess = parallelResults.Values.All(r => r.IsSuccess);
							var aggregatedResultCode = allSuccess ? "OK" : "ERROR";

							// Buscamos la siguiente transición en base al aggregatedResultCode
							if (_transitions.TryGetValue((step.Name, aggregatedResultCode), out var nextAfterParallel))
							{
								if (nextAfterParallel.TransitionType == TransitionType.Single)
								{
									currentStepName = nextAfterParallel.StepNames.First();
								}
								else if (nextAfterParallel.TransitionType == TransitionType.Parallel)
								{
									finished = true;
								}
							}
							else
							{
								finished = true;
							}
							break;
					}
				}
				else
				{
					// No hay transición => fin
					finished = true;
				}
			}

			_logger?.LogFlowEnd();
		}

		private async Task<Dictionary<string, StepResult>> ExecuteParallelSteps(FlowContext context, IEnumerable<string> stepNames)
		{
			var tasks = new Dictionary<string, Task<StepResult>>();

			foreach (var name in stepNames)
			{
				if (!_steps.ContainsKey(name))
					throw new Exception($"El step '{name}' no está registrado.");

				var step = _steps[name];

				tasks[name] = Task.Run(async () =>
				{
					try
					{
						return await step.ExecuteAsync(context);
					}
					catch (Exception ex)
					{
						_logger?.LogError($"[Parallel] Error en Step '{step.Name}'", ex);
						return new StepResult
						{
							IsSuccess = false,
							ResultCode = "ERROR",
							Message = ex.Message
						};
					}
				});
			}

			await Task.WhenAll(tasks.Values);

			var resultsDict = tasks.ToDictionary(k => k.Key, v => v.Value.Result);
			return resultsDict;
		}

		/// <summary>
		/// Limpia datos y reinicia en el Step inicial
		/// </summary>
		public async Task RestartAsync(FlowContext context)
		{
			if (_initialStep == null)
				throw new InvalidOperationException("No se ha definido Step inicial.");
			context.Data.Clear();
			await RunAsync(context);
		}
	}
}
