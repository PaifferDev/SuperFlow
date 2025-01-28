using Moq;
using SuperFlow.Core.Contracts;
using SuperFlow.Core.Default.Tools.CaptchaTool;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests.ToolsTest
{
	public class CaptchaToolTests
	{
		private Mock<IFlowLogger> mockLogger;

		public CaptchaToolTests()
        {
		 mockLogger = new Mock<IFlowLogger>();
		}

        [Fact]
		public async Task TwoProvidersFastCoincidence_ShouldReturnImmediately()
		{
			// Arrange
			var p1 = new FakeProvider("P1", "ABC", 100);   // responde en 100ms, solución=ABC
			var p2 = new FakeProvider("P2", "ABC", 120);   // 120ms, solución=ABC => coincide con p1
			var p3 = new FakeProvider("P3", "XYZ", 200);   // 200ms, solución diferente
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 5,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { p1, p2, p3 }
			};
			var tool = new CaptchaTool("TestTool", config, mockLogger.Object);

			// Act
			var context = new FlowContext();
			var result = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });

			// Assert
			Assert.NotNull(result);
			var captchaText = (string)result.GetType().GetProperty("CaptchaText").GetValue(result);
			Assert.Equal("ABC", captchaText);
		}

		[Fact]
		public async Task NoCoincidencePickBest_WhenAllDifferent()
		{
			// Arrange
			var p1 = new FakeProvider("Fast", "AAA", 100);
			var p2 = new FakeProvider("Medium", "BBB", 200);
			var p3 = new FakeProvider("Slow", "CCC", 300);
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 5,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { p1, p2, p3 }
			};
			var tool = new CaptchaTool("TestTool", config, mockLogger.Object);

			// Act
			var context = new FlowContext();
			var result = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 2 } });

			// Assert
			var captchaText = (string)result.GetType().GetProperty("CaptchaText").GetValue(result);
			Assert.Equal("AAA", captchaText); // El más rápido se selecciona.
		}

		[Fact]
		public async Task ExcludeProviderAfterTwoFails()
		{
			// Arrange
			var p1 = new TogglingProvider("P1") { NextFailsCount = 2 }; // fallará 2 veces antes de responder
			var p2 = new FakeProvider("P2", "GOOD", 150);

			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 3,
				Providers = new List<ICaptchaProvider> { p1, p2 }
			};
			var tool = new CaptchaTool("TestTool", config, mockLogger.Object);

			// Act 1: P1 falla, P2 responde correctamente.
			var context = new FlowContext();
			var result1 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });
			var captchaText1 = (string)result1.GetType().GetProperty("CaptchaText").GetValue(result1);
			Assert.Equal("GOOD", captchaText1);

			// Act 2: P1 falla otra vez, P2 sigue respondiendo.
			var result2 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });
			var captchaText2 = (string)result2.GetType().GetProperty("CaptchaText").GetValue(result2);
			Assert.Equal("GOOD", captchaText2);

			// Act 3: P1 es excluido, solo P2 responde.
			var result3 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });
			var captchaText3 = (string)result3.GetType().GetProperty("CaptchaText").GetValue(result3);
			Assert.Equal("GOOD", captchaText3);
		}

		[Fact]
		public async Task ResetProvidersWhenAllExcluded()
		{
			// Arrange
			var p1 = new FailingProvider("P1");
			var p2 = new FailingProvider("P2");
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 3,
				Providers = new List<ICaptchaProvider> { p1, p2 }
			};
			var tool = new CaptchaTool("TestTool", config, mockLogger.Object);

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(() =>
				tool.ExecuteAsync(new FlowContext(), new CaptchaToolParameters { ImageData = new byte[] { 3 } }));

			// Después de varios intentos, los contadores deben reiniciarse.
		}

		[Fact]
		public async Task ReportFailureIncreasesFailCount()
		{
			// Arrange
			var p1 = new FakeProvider("P1", "WRONG", 100);
			var p2 = new FakeProvider("P2", "CORRECT", 200);
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 5,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { p1, p2 }
			};
			var tool = new CaptchaTool("TestTool", config, mockLogger.Object);

			// Act 1: Resolver con respuesta incorrecta.
			var context = new FlowContext();
			var result = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });

			var providerName = (string)result.GetType().GetProperty("ProviderName").GetValue(result);
			Assert.Equal("P1", providerName);

			// Reportar fallo para P1.
			await tool.ReportFailureAsync("P1", "FAKE_ID_P1");

			// Act 2: Intentar nuevamente, P1 debe estar excluido.
			var result2 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });
			var providerName2 = (string)result2.GetType().GetProperty("ProviderName").GetValue(result2);
			Assert.Equal("P2", providerName2);
		}

		// ------------------------------------------------------------------------
		// Fake providers
		// ------------------------------------------------------------------------

		class FakeProvider : ICaptchaProvider
		{
			public string Name { get; }
			public double? AverageSolveTimeSeconds => null;
			public decimal? CostPerCaptcha => null;
			public int Trust => 8;
			private readonly string _solution;
			private readonly int _delayMs;

			public FakeProvider(string name, string solution, int delayMs)
			{
				Name = name;
				_solution = solution;
				_delayMs = delayMs;
			}

			public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token = default)
			{
				await Task.Delay(_delayMs, token);
				return new CaptchaResponse
				{
					CaptchaId = "FAKE_ID_" + Name,
					Solution = _solution
				};
			}

			public Task ReportFailureAsync(string captchaId)
			{
				// no-op
				return Task.CompletedTask;
			}
		}

		class FailingProvider : ICaptchaProvider
		{
			public string Name { get; }
			public double? AverageSolveTimeSeconds => null;
			public decimal? CostPerCaptcha => null;
			public int Trust => 9;
			public FailingProvider(string name)
			{
				Name = name;
			}
			public Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token = default)
			{
				throw new Exception($"{Name} fails always");
			}

			public Task ReportFailureAsync(string captchaId)
			{
				return Task.CompletedTask;
			}
		}

		/// <summary>
		/// Este provider falla "NextFailsCount" veces consecutivas, 
		/// luego da "GOOD" como solución
		/// </summary>
		class TogglingProvider : ICaptchaProvider
		{
			public string Name { get; }
			public double? AverageSolveTimeSeconds => null;
			public decimal? CostPerCaptcha => null;
			public int Trust => 7;
			public int NextFailsCount { get; set; } = 0;
			public TogglingProvider(string name)
			{
				Name = name;
			}

			public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token)
			{
				await Task.Delay(100, token);
				if (NextFailsCount > 0)
				{
					NextFailsCount--;
					throw new Exception($"{Name} intencional fail, leftFails={NextFailsCount}");
				}
				return new CaptchaResponse
				{
					CaptchaId = "FAKE_ID_" + Name,
					Solution = "GOOD"
				};
			}

			public Task ReportFailureAsync(string captchaId)
			{
				return Task.CompletedTask;
			}
		}
	}
}
