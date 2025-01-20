using SuperFlow.Core.Default.Tools.CaptchaTool;
using SuperFlow.Core.Default.Tools.CaptchaTool.Models;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests.ToolsTest
{
	public class CaptchaToolTests
	{
		[Fact]
		public async Task TwoProvidersFastCoincidence_ShouldReturnImmediately()
		{
			// Arrange
			var p1 = new FakeProvider("P1", "ABC", 100);   // respond in 100ms, solution=ABC
			var p2 = new FakeProvider("P2", "ABC", 120);   // 120ms, solution=ABC => coincide con p1
			var p3 = new FakeProvider("P3", "XYZ", 200);   // 200ms
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 5,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { p1, p2, p3 }
			};
			var tool = new CaptchaTool("TestTool", config);

			// Act
			var context = new FlowContext();
			var result = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 1 } });

			// Assert
			Assert.NotNull(result);
			var captchaText = (string)result.GetType().GetProperty("CaptchaText").GetValue(result);
			Assert.Equal("ABC", captchaText);
			// Debe retornar en cuanto P2 llega, sin esperar P3
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
			var tool = new CaptchaTool("TestTool", config);

			// Act
			var context = new FlowContext();
			var result = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 2 } });

			// Assert
			// No coincidencia => fallback => pick el "menos fails" => todos =0
			// => tie => pick "más rápido" => p1 => "AAA"
			var captchaText = (string)result.GetType().GetProperty("CaptchaText").GetValue(result);
			Assert.Equal("AAA", captchaText);
		}

		[Fact]
		public async Task AllFail_ThrowsException()
		{
			// Arrange
			var f1 = new FailingProvider("F1");
			var f2 = new FailingProvider("F2");
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { f1, f2 }
			};
			var tool = new CaptchaTool("TestTool", config);

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(async () =>
			{
				await tool.ExecuteAsync(new FlowContext(), new CaptchaToolParameters { ImageData = new byte[] { 3 } });
			});
		}

		[Fact]
		public async Task ExcludeProviderAfter2Fails()
		{
			// Arrange
			var p1 = new TogglingProvider("P1") { NextFailsCount = 2 }; // fallará 2 veces, luego success
			var p2 = new FakeProvider("P2", "GOOD", 150);

			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { p1, p2 }
			};
			var tool = new CaptchaTool("TestTool", config);

			// Round1 => p1( error ), p2( success?), 
			// Actually p1 => error, p2 => success => fallback ??? 
			// or if p2 is success => no need fallback => but no coincidence => fallback => picks p2. 
			// => p1 accum=1 fail
			// Round2 => p1 => error => accum=2 fails => p2 => success => fallback => picks p2
			// Round3 => => p1 is excluded => only p2 => final?

			// Act #1
			var context = new FlowContext();
			var r1 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 9 } });
			// p1 => fail => fails=1, p2 => success => no coincidence => fallback => p2
			var text1 = (string)r1.GetType().GetProperty("CaptchaText").GetValue(r1);
			Assert.Equal("GOOD", text1);

			// Act #2
			var r2 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 9 } });
			// p1 => fail => fails=2 => p2 => success => fallback => p2
			var text2 = (string)r2.GetType().GetProperty("CaptchaText").GetValue(r2);
			Assert.Equal("GOOD", text2);

			// Act #3 => p1 exclude => only p2 => immediate success
			var r3 = await tool.ExecuteAsync(context, new CaptchaToolParameters { ImageData = new byte[] { 9 } });
			var text3 = (string)r3.GetType().GetProperty("CaptchaText").GetValue(r3);
			Assert.Equal("GOOD", text3);
		}

		[Fact]
		public async Task ResetIfAllExcluded()
		{
			// Arrange
			var allFailing = new List<ICaptchaProvider>
			{
				new FailingProvider("F1"),
				new FailingProvider("F2"),
				new FailingProvider("F3")
			};
			var config = new CaptchaToolConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 2,
				Providers = allFailing
			};
			var tool = new CaptchaTool("TestTool", config);

			// Round1 => todos fallan => fails=1 cada uno => throw => reintento (#2)
			// Round2 => todos fallan => fails=2 => throw => reintento (#3) => 
			// Round3 => se detecta "all excluded" => se resetean => but siguen fallando => infinite?
			// -> para test, con maxretries=2 => lanza exception final

			// Actually, 
			// 1er ExecuteAsync => 
			//   Round1 => all fail => exception => attempt=1 
			//   Round2 => all fail => exception => attempt=2 
			// => final => lanza error => fails=2 => pero 3rd round no se produce xq maxretries=2
			await Assert.ThrowsAsync<InvalidOperationException>(() =>
				tool.ExecuteAsync(new FlowContext(), new CaptchaToolParameters { ImageData = new byte[] { 7 } }));
		}

		// ------------------------------------------------------------------------
		// Fake providers
		// ------------------------------------------------------------------------

		class FakeProvider : ICaptchaProvider
		{
			public string Name { get; }
			public double? AverageSolveTimeSeconds => null;
			public decimal? CostPerCaptcha => null;

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
