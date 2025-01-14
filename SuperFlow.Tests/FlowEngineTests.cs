using SuperFlow.Core;
using SuperFlow.Core.Contracts;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests
{
	public class FlowEngineTests
	{
		[Fact]
		public async Task RunAsync_Should_ThrowException_When_NoInitialStepDefined()
		{
			// Arrange
			var logger = new TestLogger();
			var engine = new FlowEngine(logger);

			var context = new FlowContext();

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunAsync(context));
		}

		[Fact]
		public async Task RunAsync_Should_ExecuteInitialStep_And_EndIfNoTransition()
		{
			// Arrange
			var logger = new TestLogger();
			var engine = new FlowEngine(logger);

			var step = new TestStep("Step1", "OK");
			engine.RegisterStep(step, isInitial: true);

			var context = new FlowContext();

			// Act
			await engine.RunAsync(context);

			// Assert
			Assert.True(step.Executed, "The initial step should have been executed");
			// No transitions => flow ends
		}

		[Fact]
		public async Task RunAsync_Should_FollowTransition_When_ResultCodeMatches()
		{
			// Arrange
			var logger = new TestLogger();
			var engine = new FlowEngine(logger);

			var step1 = new TestStep("Step1", "OK");
			var step2 = new TestStep("Step2", "OK");
			engine.RegisterStep(step1, isInitial: true);
			engine.RegisterStep(step2);

			engine.SetTransition("Step1", "OK", "Step2");

			var context = new FlowContext();

			// Act
			await engine.RunAsync(context);

			// Assert
			Assert.True(step1.Executed, "Step1 must be executed");
			Assert.True(step2.Executed, "Step2 must be executed after Step1");
		}

		[Fact]
		public async Task RunAsync_Should_ExecuteStepsInParallel()
		{
			// Arrange
			var logger = new TestLogger();
			var engine = new FlowEngine(logger);

			var step1 = new TestStep("ParallelStep1", "OK");
			var step2 = new TestStep("ParallelStep2", "OK");
			var initialStep = new TestStep("Initial", "PARALLEL");

			engine.RegisterStep(initialStep, isInitial: true);
			engine.RegisterStep(step1);
			engine.RegisterStep(step2);

			// Definimos que si "Initial" retorna PARALLEL, ejecute step1 y step2 en paralelo
			engine.SetParallelTransition("Initial", "PARALLEL", "ParallelStep1", "ParallelStep2");

			var context = new FlowContext();

			// Act
			await engine.RunAsync(context);

			// Assert
			Assert.True(initialStep.Executed);
			Assert.True(step1.Executed);
			Assert.True(step2.Executed);
		}
	}

	// Ejemplo de Step para test
	internal class TestStep : BaseStep
	{
		private string _forcedResultCode;
		public bool Executed { get; private set; } = false;

		public TestStep(string name, string forcedResultCode) : base(name)
		{
			_forcedResultCode = forcedResultCode;
		}

		public override async Task<StepResult> ExecuteAsync(FlowContext context)
		{
			Executed = true;
			await Task.Delay(50); // simulate small work
			return new StepResult
			{
				IsSuccess = true,
				ResultCode = _forcedResultCode
			};
		}
	}

	// Logger de prueba
	internal class TestLogger : IFlowLogger
	{
		public void LogFlowStart() { }
		public void LogFlowEnd() { }
		public void LogStepStart(string stepName) { }
		public void LogStepEnd(string stepName, StepResult result) { }
		public void LogParallelStepStart(IEnumerable<string> stepNames) { }
		public void LogParallelStepEnd(Dictionary<string, StepResult> results) { }
		public void LogError(string message, Exception? ex = null) { }
	}
}
