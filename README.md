# SuperFlow

[![NuGet Version](https://img.shields.io/nuget/v/SuperFlow.svg)](https://www.nuget.org/packages/SuperFlow)
[![Build Status](https://img.shields.io/github/actions/workflow/status/PaifferDev/SuperFlow/build.yml?branch=main)](https://github.com/PaifferDev/SuperFlow/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**SuperFlow** is a robust .NET library designed to **orchestrate bots** and **automate processes** through a flexible **FlowEngine** and a system of **Steps** and **Actions**. SuperFlow empowers developers to create scalable, maintainable, and testable automation workflows with ease.

---

## 🌟 Features

- **FlowEngine**: Manage and execute Steps sequentially or in parallel, handling states and transitions seamlessly.
- **Steps**: Define discrete units of work (e.g., downloading a captcha, processing data, calling an API) reusable across different flows.
- **Actions**: Perform generic and reusable operations such as making HTTP requests, sending Telegram messages, resolving captchas, and more.
- **FlowContext**: A shared container that travels through Steps, allowing injection of services like `DbContext`, `HttpClientFactory`, configuration parameters, and intermediate data.
- **Dependency Injection**: Integrates smoothly with `IServiceCollection`, facilitating service and configuration management.
- **Resilience**: Incorporates Polly for handling retries, circuit breakers, and other resilience strategies.
- **Unit Testing**: Designed for testability, enabling easy mocking of HTTP requests and other external dependencies.
- **Continuous Integration**: Automated builds and deployments using GitHub Actions, ensuring reliable and consistent releases.

---

## 📦 Installation

Install **SuperFlow** via **NuGet**:

### Using .NET CLI

🚀 Quick Start

A simple example to get you started with SuperFlow:
1. Configure Dependency Injection

using Microsoft.Extensions.DependencyInjection;
using SuperFlow.Core;
using SuperFlow.Core.Default.Actions; // Includes RequestAction, TelegramAction, CaptchaAction, etc.
using SuperFlow.Actions;
using System.Collections.Generic;
using System.Net.Http;

public class Program
{
    public static async Task Main()
    {
        // 1. Set up the service collection
        var services = new ServiceCollection();
        services.AddSuperFlowCore(); // Registers FlowEngine and core services

        // 2. Register HttpClient
        services.AddHttpClient("GenericClient");

        // 3. Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // 4. Register Actions
        ActionRegistry.RegisterAction(new RequestAction("HttpRequestAction", new RequestActionConfig
        {
            BaseUrl = "https://api.example.com",
            DefaultHeaders = new Dictionary<string, string>
            {
                { "Authorization", "Bearer YOUR_API_KEY" }
            }
        }, serviceProvider.GetRequiredService<IHttpClientFactory>()));

        ActionRegistry.RegisterAction(new TelegramAction("SendTelegramAction", new TelegramConfig
        {
            BotApiKey = "YOUR_TELEGRAM_BOT_API_KEY",
            DefaultChatId = "YOUR_DEFAULT_CHAT_ID"
        }));

        ActionRegistry.RegisterAction(new CaptchaAction("SolveCaptchaAction", new CaptchaActionConfig
        {
            SolveTimeoutSeconds = 70,
            MaxRetries = 3,
            Providers = new List<ICaptchaProvider>
            {
                new TwoCaptchaProvider(new HttpClient(), new[] { "YOUR_2CAPTCHA_API_KEY" }),
                new CapMonsterProvider(new HttpClient(), "YOUR_CAPMONSTER_API_KEY")
                // Add more providers as needed
            }
        }));

        // 5. Create FlowEngine
        var engine = serviceProvider.GetRequiredService<FlowEngine>();

        // 6. Register Steps
        engine.RegisterStep(new StepDownloadCaptcha("DownloadCaptcha"), isInitial: true);
        engine.RegisterStep(new StepSolveCaptcha("SolveCaptcha"));
        engine.RegisterStep(new StepSendTelegramMessage("SendMessage"));

        // 7. Define Transitions
        engine.SetTransition("DownloadCaptcha", "OK", "SolveCaptcha");
        engine.SetTransition("SolveCaptcha", "OK", "SendMessage");

        // 8. Create FlowContext and Execute
        var context = new FlowContext { ServiceProvider = serviceProvider };
        await engine.RunAsync(context);

        Console.WriteLine("Flow completed successfully.");
    }
}

// Example Step Implementations

public class StepDownloadCaptcha : BaseStep
{
    public StepDownloadCaptcha(string name) : base(name) { }

    public override async Task<StepResult> ExecuteAsync(FlowContext context)
    {
        // Use RequestAction to download captcha image
        var requestAction = ActionRegistry.GetAction("HttpRequestAction");
        var response = await requestAction.ExecuteAsync(context, new { Method = "GET", Endpoint = "get-captcha.jpg" });

        // Assume response.Body contains the image bytes in Base64
        byte[] captchaImage = Convert.FromBase64String(response.Body);
        context.Data["CaptchaImage"] = captchaImage;

        return new StepResult { IsSuccess = true, ResultCode = "OK" };
    }
}

public class StepSolveCaptcha : BaseStep
{
    public StepSolveCaptcha(string name) : base(name) { }

    public override async Task<StepResult> ExecuteAsync(FlowContext context)
    {
        var captchaAction = ActionRegistry.GetAction("SolveCaptchaAction");
        byte[] captchaImage = context.Data["CaptchaImage"] as byte[] ?? throw new Exception("Captcha image not found.");

        var result = await captchaAction.ExecuteAsync(context, new { ImageData = captchaImage });
        context.Data["CaptchaSolution"] = result.CaptchaText;

        return new StepResult { IsSuccess = true, ResultCode = "OK" };
    }
}

public class StepSendTelegramMessage : BaseStep
{
    public StepSendTelegramMessage(string name) : base(name) { }

    public override async Task<StepResult> ExecuteAsync(FlowContext context)
    {
        var telegramAction = ActionRegistry.GetAction("SendTelegramAction");
        string message = context.Data["CaptchaSolution"] as string ?? "Default message.";

        var result = await telegramAction.ExecuteAsync(context, new { Message = $"Captcha solved: {message}" });
        context.Data["TelegramResponse"] = result;

        return new StepResult { IsSuccess = true, ResultCode = "OK" };
    }
}

🧩 Detailed Usage
Actions

Actions are reusable operations invoked by Steps. Examples include HTTP requests, sending Telegram messages, and solving captchas.
RequestAction

Handles HTTP requests.

using SuperFlow.Actions;
using SuperFlow.Core.Default.Actions.RequestAction.Models;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Actions.RequestAction
{
    public class RequestAction : BaseAction
    {
        private readonly RequestActionConfig _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public RequestAction(string name, RequestActionConfig config, IHttpClientFactory httpClientFactory)
            : base(name)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
        {
            string method = parameters?.Method ?? "GET";
            string endpoint = parameters?.Endpoint ?? string.Empty;
            string body = parameters?.Body ?? string.Empty;

            var client = _httpClientFactory.CreateClient("GenericClient");
            string url = $"{_config.BaseUrl.TrimEnd('/')}/{endpoint}";

            HttpResponseMessage response;
            if (method.ToUpperInvariant() == "POST")
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                response = await client.PostAsync(url, content);
            }
            else
            {
                response = await client.GetAsync(url);
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            return new
            {
                StatusCode = (int)response.StatusCode,
                Body = responseBody
            };
        }
    }
}

TelegramAction

Sends messages via Telegram.

using SuperFlow.Core.Actions;
using SuperFlow.Core.Default.Actions.TelegramAction.Models;
using SuperFlow.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Threading.Tasks;

namespace SuperFlow.Core.Default.Actions.TelegramAction
{
    public class TelegramAction : BaseAction
    {
        private readonly TelegramConfig _config;

        public TelegramAction(string name, TelegramConfig config) : base(name)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
        {
            string message = parameters?.Message ?? "Default message";
            string chatId = parameters?.ChatId ?? _config.DefaultChatId;

            var botClient = CreateBotClient(_config.BotApiKey);
            var response = await SendMessageAsync(botClient, chatId, message);

            return new
            {
                MessageId = response.MessageId,
                Chat = response.Chat.Id
            };
        }

        protected virtual ITelegramBotClient CreateBotClient(string apiKey)
        {
            return new TelegramBotClient(apiKey);
        }

        protected virtual Task<Message> SendMessageAsync(ITelegramBotClient botClient, string chatId, string text)
        {
            return botClient.SendMessageAsync(long.Parse(chatId), text);
        }
    }
}

CaptchaAction

Resolves captchas using multiple providers with retry logic.

using SuperFlow.Actions;
using SuperFlow.Core.Default.Actions.CaptchaAction.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace SuperFlow.Core.Default.Actions.CaptchaAction
{
    public class CaptchaAction : BaseAction
    {
        private readonly CaptchaActionConfig _config;
        private readonly ConcurrentDictionary<string, int> _providerFailureCounts = new ConcurrentDictionary<string, int>();

        public CaptchaAction(string name, CaptchaActionConfig config) : base(name)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            if (_config.Providers.Count == 0)
                throw new InvalidOperationException("No captcha providers registered in CaptchaActionConfig.");
        }

        public override async Task<object?> ExecuteAsync(FlowContext context, dynamic? parameters = null)
        {
            byte[]? imageData = parameters?.ImageData;
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("No 'ImageData' received to solve captcha.");
            }

            var result = await SolveCaptchaWithRetriesAsync(imageData);

            return new 
            {
                ProviderName = result.ProviderName,
                CaptchaText = result.CaptchaText,
                SolveTimeSeconds = result.SolveTimeSeconds,
                CaptchaId = result.CaptchaId
            };
        }

        private async Task<CaptchaResult> SolveCaptchaWithRetriesAsync(byte[] imageData)
        {
            int attempt = 0;
            while (attempt < _config.MaxRetries)
            {
                attempt++;
                try
                {
                    return await SolveCaptchaOnceAsync(imageData, _config.SolveTimeoutSeconds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CaptchaAction] Attempt #{attempt} failed: {ex.Message}");
                }
            }

            throw new InvalidOperationException($"Failed to solve captcha after {_config.MaxRetries} attempts.");
        }

        private async Task<CaptchaResult> SolveCaptchaOnceAsync(byte[] imageData, int solveTimeoutSeconds)
        {
            var providers = _config.Providers
                .OrderBy(p => _providerFailureCounts.GetValueOrDefault(p.Name, 0))
                .ToList();

            var tasks = providers.Select(provider => SolveWithProvider(provider, imageData, solveTimeoutSeconds)).ToList();

            while (tasks.Count > 0)
            {
                Task<CaptchaResult> finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                try
                {
                    var result = await finishedTask; 
                    return result;
                }
                catch (TimeoutException tex)
                {
                    IncrementFailureFromMessage(tex.Message);
                }
                catch (Exception ex)
                {
                    IncrementFailureFromMessage(ex.Message);
                }
            }

            throw new InvalidOperationException("All captcha providers failed to solve the captcha.");
        }

        private async Task<CaptchaResult> SolveWithProvider(ICaptchaProvider provider, byte[] imageData, int solveTimeoutSeconds)
        {
            var stopwatch = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(solveTimeoutSeconds));

            try
            {
                var providerResponse = await provider.SolveCaptchaAsync(imageData, cts.Token);
                stopwatch.Stop();

                return new CaptchaResult(
                    providerName: provider.Name,
                    captchaText: providerResponse.Solution,
                    solveTimeSeconds: stopwatch.Elapsed.TotalSeconds,
                    captchaId: providerResponse.CaptchaId
                );
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                throw new TimeoutException($"[CaptchaAction] Timeout after {solveTimeoutSeconds}s with provider {provider.Name}");
            }
            catch
            {
                stopwatch.Stop();
                throw;
            }
        }

        private void IncrementFailureFromMessage(string message)
        {
            var prefix = "provider ";
            int index = message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                string providerName = message.Substring(index + prefix.Length).Trim();
                _providerFailureCounts.AddOrUpdate(providerName, 1, (k, oldV) => oldV + 1);
            }
        }

        public async Task ReportFailureAsync(string providerName, string captchaId)
        {
            var provider = _config.Providers.FirstOrDefault(p => p.Name == providerName);
            if (provider != null)
            {
                await provider.ReportFailureAsync(captchaId);
                _providerFailureCounts.AddOrUpdate(providerName, 1, (k, oldV) => oldV + 1);
            }
        }
    }
}


📝 License

Distributed under the MIT License. See LICENSE for more information.
🤝 Contributing

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

    Fork the Project
    Create your Feature Branch (git checkout -b feature/AmazingFeature)
    Commit your Changes (git commit -m 'Add some AmazingFeature')
    Push to the Branch (git push origin feature/AmazingFeature)
    Open a Pull Request

📫 Contact

https://x.com/PaifferDev

Project Link: https://github.com/PaifferDev/SuperFlow
📚 Additional Documentation

For more detailed documentation, examples, and advanced configurations, please refer to the Wiki or open an issue on the GitHub repository.

Thank you for using SuperFlow!
If you find this library useful, please give it a ⭐ on GitHub and consider leaving a review on NuGet.
