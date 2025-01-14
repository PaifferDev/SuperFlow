using Xunit;
using SuperFlow.Core;
using System.Threading.Tasks;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests
{
	public class StepExampleTests
	{
		[Fact]
		public async Task StepExample_Should_ReturnOK_When_Executed()
		{
			// Arrange
			var step = new StepExample("StepExampleTest");
			var context = new FlowContext();

			// Act
			var result = await step.ExecuteAsync(context);

			// Assert
			Assert.True(result.IsSuccess);
			Assert.Equal("OK", result.ResultCode);
		}
	}

	// Supón que definiste este StepExample en tu librería
	internal class StepExample : BaseStep
	{
		public StepExample(string name) : base(name) { }

		public override Task<StepResult> ExecuteAsync(FlowContext context)
		{
			// Lógica simulada
			return Task.FromResult(new StepResult
			{
				IsSuccess = true,
				ResultCode = "OK",
				Message = "Prueba exitosa"
			});
		}
	}
}
