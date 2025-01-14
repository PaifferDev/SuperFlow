using SuperFlow.Core.Default.Actions.CaptchaAction;
using SuperFlow.Core.Default.Actions.CaptchaAction.Models;
using SuperFlow.Core.Models;

namespace SuperFlow.Tests.ActionsTest
{
	public class CaptchaActionTests
	{
		[Fact]
		public async Task ExecuteAsync_Should_ReturnFirstSuccessfulProvider()
		{
			// Arrange
			var fastProvider = new FakeCaptchaProvider("FastProvider")
			{
				// Config: success instantly
				WillSucceed = true,
				DelayMs = 100
			};
			var slowProvider = new FakeCaptchaProvider("SlowProvider")
			{
				WillSucceed = true,
				DelayMs = 1000
			};

			var config = new CaptchaActionConfig
			{
				SolveTimeoutSeconds = 5,
				MaxRetries = 1, // un intento
				Providers = new List<ICaptchaProvider> { slowProvider, fastProvider }
			};

			var action = new CaptchaAction("SolveCaptchaAction", config);
			var context = new FlowContext();

			var parameters = new CaptchaActionParameters
			{
				ImageData = new byte[] { 1, 2, 3 } // simular un captcha
			};

			// Act
			var result = await action.ExecuteAsync(context, parameters);

			// Assert
			Assert.NotNull(result);
			// result => { ProviderName, CaptchaText, etc. }
			string providerName = (string)result.GetType().GetProperty("ProviderName")!.GetValue(result);
			string captchaText = (string)result.GetType().GetProperty("CaptchaText")!.GetValue(result);

			// Debería ser el "FastProvider" (100ms) antes que el "SlowProvider" (1000ms)
			Assert.Equal("FastProvider", providerName);
			Assert.Equal("FAKE_SOLUTION", captchaText);
		}

		[Fact]
		public async Task ExecuteAsync_Should_ThrowIfAllProvidersFail()
		{
			// Arrange
			var failingProvider1 = new FakeCaptchaProvider("Fail1")
			{
				WillSucceed = false
			};
			var failingProvider2 = new FakeCaptchaProvider("Fail2")
			{
				WillSucceed = false
			};

			var config = new CaptchaActionConfig
			{
				SolveTimeoutSeconds = 2,
				MaxRetries = 1,
				Providers = new List<ICaptchaProvider> { failingProvider1, failingProvider2 }
			};

			var action = new CaptchaAction("SolveCaptchaAction", config);
			var context = new FlowContext();

			var parameters = new CaptchaActionParameters { ImageData = new byte[] { 9, 9, 9 } };

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => action.ExecuteAsync(context, parameters));
		}

		[Fact]
		public async Task ExecuteAsync_Should_Retry_When_FirstAttemptFails()
		{
			// Arrange
			// El provider fallará la 1a vez, pero en el 2o reintento funcionará
			var togglingProvider = new FakeCaptchaProvider("ToggleProvider")
			{
				WillSucceed = false,
				DelayMs = 100
			};

			var config = new CaptchaActionConfig
			{
				SolveTimeoutSeconds = 1,
				MaxRetries = 2,
				Providers = new List<ICaptchaProvider> { togglingProvider }
			};

			var action = new CaptchaAction("SolveCaptchaAction", config);
			var context = new FlowContext();
			var parameters = new CaptchaActionParameters { ImageData = new byte[] { 7, 7, 7 } };

			// 1er reintento => falla
			// 2do reintento => lo hacemos que "ahora sí funcione"
			// => Ver FakeCaptchaProvider: en cada intento cambia "WillSucceed"

			// Act
			var result = await action.ExecuteAsync(context, parameters);

			// Assert
			// Debe acabar con éxito en el 2o reintento
			string providerName = (string)result.GetType().GetProperty("ProviderName")!.GetValue(result);
			Assert.Equal("ToggleProvider", providerName);
		}

		//---------------------------------------------------------------------------------
		// Fake provider
		//---------------------------------------------------------------------------------
		private class FakeCaptchaProvider : ICaptchaProvider
		{
			public string Name { get; }
			public double? AverageSolveTimeSeconds => null;
			public decimal? CostPerCaptcha => null;

			public bool WillSucceed { get; set; } = true;
			public int DelayMs { get; set; } = 500;

			// Podríamos usar un toggle para simular que falla la 1ra vez, luego succeed

			public FakeCaptchaProvider(string name)
			{
				Name = name;
			}

			public async Task<CaptchaResponse> SolveCaptchaAsync(byte[] imageData, CancellationToken token)
			{
				// Simulamos un delay
				await Task.Delay(DelayMs, token);

				if (!WillSucceed)
				{
					// Cambiamos WillSucceed para un posible 2o reintento
					WillSucceed = true;
					throw new Exception($"Provider {Name} ha fallado intencionalmente.");
				}

				// Caso de éxito
				return new CaptchaResponse
				{
					CaptchaId = "FAKE_ID",
					Solution = "FAKE_SOLUTION"
				};
			}

			public Task ReportFailureAsync(string captchaId)
			{
				// No hacemos nada, simulado
				Console.WriteLine($"[FakeCaptchaProvider] ReportFailure {Name}, captchaId={captchaId}");
				return Task.CompletedTask;
			}
		}
	}
}
