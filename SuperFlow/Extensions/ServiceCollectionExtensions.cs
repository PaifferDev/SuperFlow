using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using SuperFlow.Core;
using SuperFlow.Core.Contracts;
using SuperFlow.Core.Default.Tools.CaptchaTool;
using SuperFlow.Core.Default.Tools.RequestTool;
using SuperFlow.Core.Default.Tools.SNSTool;
using SuperFlow.Core.Default.Tools.SQSTool;
using SuperFlow.Core.Default.Tools.TelegramTool;
using SuperFlow.Core.Tools;

namespace SuperFlow.Extensions
{
	/// <summary>
	/// Extensiones para registrar herramientas y servicios en el contenedor de dependencias.
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Añade todas las herramientas de SuperFlow al contenedor de servicios.
		/// </summary>
		/// <param name="services">Contenedor de servicios.</param>
		/// <param name="configuration">Configuración de la aplicación.</param>
		/// <returns>Contenedor de servicios actualizado.</returns>
		public static IServiceCollection AddSuperFlowCore(this IServiceCollection services, IConfiguration configuration)
		{
			// 1. Registrar el Logger por defecto
			services.TryAddSingleton<IFlowLogger, ConsoleFlowLogger>();

			// 2. Registrar FlowEngine con el logger inyectado
			services.AddSingleton<FlowEngine>(sp =>
			{
				var logger = sp.GetRequiredService<IFlowLogger>();
				return new FlowEngine(logger);
			});

			// 3. Registrar un HttpClient con Polly para reintentos
			var retryPolicy = HttpPolicyExtensions
				.HandleTransientHttpError()
				.WaitAndRetryAsync(
					retryCount: 3,
					sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
				);

			services.AddHttpClient("GenericClient")
					.SetHandlerLifetime(TimeSpan.FromMinutes(2))
					.AddPolicyHandler(retryPolicy);

			// 4. Eliminar filtros de handlers si es necesario
			services.RemoveAll<IHttpMessageHandlerBuilderFilter>();

			// 5. Registrar herramientas existentes
			services.AddSingleton<IFlowTool, CaptchaTool>();
			services.AddSingleton<IFlowTool, RequestTool>();
			services.AddSingleton<IFlowTool, TelegramTool>();

			// 6. Registrar SNSTool y sus dependencias
			var snsConfig = configuration.GetSection("SNSTool").Get<SNSToolConfig>();
			services.AddSingleton<ISNSSender>(provider =>
			{
				var snsClient = provider.GetRequiredService<IAmazonSimpleNotificationService>();
				return new SNSSender(snsClient, snsConfig);
			});
			services.AddSingleton<IFlowTool>(provider =>
				new SNSTool("SNSTool", provider.GetRequiredService<ISNSSender>()));

			// 7. Registrar SQSTool y sus dependencias
			var sqsConfig = configuration.GetSection("SQSTool").Get<SQSToolConfig>();
			services.AddSingleton<ISQSReceiver>(provider =>
			{
				var sqsClient = provider.GetRequiredService<IAmazonSQS>();
				return new SQSReceiver(sqsClient, sqsConfig);
			});
			services.AddSingleton<IFlowTool>(provider =>
				new SQSTool("SQSTool", provider.GetRequiredService<ISQSReceiver>()));

			// 8. Registrar servicios de AWS SNS y SQS
			services.AddAWSService<IAmazonSimpleNotificationService>();
			services.AddAWSService<IAmazonSQS>();

			// 9. Registrar ToolRegistry como una instancia singleton
			services.AddSingleton<ToolRegistry>();

			return services;
		}
	}
}
