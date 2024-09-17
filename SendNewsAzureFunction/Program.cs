using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NewsNotifier.Service;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration(configBuilder =>
    {
        configBuilder.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        configBuilder.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddHttpClient();
        services.AddLogging();

        var config = context.Configuration;

        // Configure ServiceBusClient and ServiceBusSender
        var serviceBusConnection = config["ServiceBusConnection"];
        var queueName = config["ServiceBus:QueueName"];
        services.AddSingleton<ServiceBusClient>(new ServiceBusClient(serviceBusConnection));
        services.AddSingleton<ServiceBusSender>(sp =>
            sp.GetRequiredService<ServiceBusClient>().CreateSender(queueName));

        // Register NewsProcessorService
        services.AddSingleton<INewsProcessorService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<NewsProcessorService>>();
            var apiBaseUrl = config["WebApiBaseUrl"];
            var serviceBusSender = sp.GetRequiredService<ServiceBusSender>();

            return new NewsProcessorService(
                httpClientFactory.CreateClient(),
                logger,
                apiBaseUrl,
                serviceBusSender);
        });
    })
    .ConfigureFunctionsWorkerDefaults(workerApplication =>
    {
        workerApplication.UseFunctionExecutionMiddleware();
    })
    .Build();

host.Run();