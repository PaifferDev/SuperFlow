using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using SuperFlow.Core;
using Polly;
using SuperFlow.Core.Contracts;

namespace SuperFlow.Extensions
{
    public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registra en DI: 
		///  1) FlowEngine (Singleton) con un Logger por defecto (ConsoleFlowLogger)
		///  2) HttpClient de ejemplo con Polly (para reintentos).
		/// </summary>
		public static IServiceCollection AddSuperFlowCore(this IServiceCollection services)
		{
			// Logger por defecto
			services.TryAddSingleton<IFlowLogger, ConsoleFlowLogger>();

			// FlowEngine con logger inyectado
			services.AddSingleton<FlowEngine>(sp =>
			{
				var logger = sp.GetService<IFlowLogger>();
				return new FlowEngine(logger);
			});

			// Registrar un HttpClient con Polly (opcional)
			var retryPolicy = Polly.Policy
				.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
				.WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

			services.AddHttpClient("GenericClient")
					.SetHandlerLifetime(TimeSpan.FromMinutes(2))
					.AddPolicyHandler(retryPolicy);

			services.RemoveAll<IHttpMessageHandlerBuilderFilter>();

			return services;
		}
	}
}
