using System;
using SuperFlow.Core.Contracts;
using SuperFlow.Core.Models;

namespace SuperFlow.Core
{
    /// <summary>
    /// Logger simple que imprime por consola. 
    /// </summary>
    public class ConsoleFlowLogger : IFlowLogger
	{
		public void LogFlowStart()
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Log.Information($"[FlowLogger] Inicio del flujo => {DateTime.Now}");
			Console.ResetColor();
		}

		public void LogFlowEnd()
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Log.Information($"[FlowLogger] Fin del flujo => {DateTime.Now}");
			Console.ResetColor();
		}

		public void LogStepStart(string stepName)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Log.Information($"[FlowLogger] Iniciando Step: {stepName}");
			Console.ResetColor();
		}

		public void LogStepEnd(string stepName, StepResult result)
		{
			Console.ForegroundColor = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
			Log.Information($"[FlowLogger] Step {stepName} finalizado => Code: {result.ResultCode}, Success: {result.IsSuccess}, Msg: {result.Message}");
			Console.ResetColor();
		}

		public void LogParallelStepStart(IEnumerable<string> stepNames)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Log.Information($"[FlowLogger] Ejecutando Steps en paralelo: {string.Join(", ", stepNames)}");
			Console.ResetColor();
		}

		public void LogParallelStepEnd(Dictionary<string, StepResult> results)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Log.Information($"[FlowLogger] Resultados Steps en paralelo:");
			foreach (var kvp in results)
			{
				var stepName = kvp.Key;
				var result = kvp.Value;
				Log.Information($"   - {stepName}: Code={result.ResultCode}, Success={result.IsSuccess}, Msg={result.Message}");
			}
			Console.ResetColor();
		}

		public void LogError(string message, Exception? ex = null)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Log.Information($"[FlowLogger][ERROR] {message}. Ex: {ex?.Message}");
			Console.ResetColor();
		}
	}
}
