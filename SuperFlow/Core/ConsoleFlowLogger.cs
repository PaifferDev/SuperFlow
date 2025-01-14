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
			Console.WriteLine($"[FlowLogger] Inicio del flujo => {DateTime.Now}");
			Console.ResetColor();
		}

		public void LogFlowEnd()
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine($"[FlowLogger] Fin del flujo => {DateTime.Now}");
			Console.ResetColor();
		}

		public void LogStepStart(string stepName)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"[FlowLogger] Iniciando Step: {stepName}");
			Console.ResetColor();
		}

		public void LogStepEnd(string stepName, StepResult result)
		{
			Console.ForegroundColor = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
			Console.WriteLine($"[FlowLogger] Step {stepName} finalizado => Code: {result.ResultCode}, Success: {result.IsSuccess}, Msg: {result.Message}");
			Console.ResetColor();
		}

		public void LogParallelStepStart(IEnumerable<string> stepNames)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"[FlowLogger] Ejecutando Steps en paralelo: {string.Join(", ", stepNames)}");
			Console.ResetColor();
		}

		public void LogParallelStepEnd(Dictionary<string, StepResult> results)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"[FlowLogger] Resultados Steps en paralelo:");
			foreach (var kvp in results)
			{
				var stepName = kvp.Key;
				var result = kvp.Value;
				Console.WriteLine($"   - {stepName}: Code={result.ResultCode}, Success={result.IsSuccess}, Msg={result.Message}");
			}
			Console.ResetColor();
		}

		public void LogError(string message, Exception? ex = null)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"[FlowLogger][ERROR] {message}. Ex: {ex?.Message}");
			Console.ResetColor();
		}
	}
}
