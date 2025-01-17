using SuperFlow.Core.Models;
using SuperFlow.Core.Tools;

namespace SuperFlow.Core.Default.Tools.TimerTool
{
	public class TimerTool : BaseTool
	{
		public TimerTool(string name) : base(name)
		{
		}

		public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
		{
			var args = parameters as TimerToolParameters;
			if (args == null)
				throw new ArgumentException("Se requieren parámetros de tipo TimerToolParameters");

			await Task.Delay(args.DelayMilliseconds);
			return new { Status = "Timer Completed", Delay = args.DelayMilliseconds };
		}
	}

	public class TimerToolParameters
	{
		public int DelayMilliseconds { get; set; } = 1000;
	}
}
