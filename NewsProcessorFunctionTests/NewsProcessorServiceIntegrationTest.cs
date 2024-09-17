using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewsNotifier.DataLayer;
using NewsNotifier.Service;
using Xunit;

namespace NewsNotifier.Tests.Integration
{
    public class NewsProcessorServiceIntegrationTests : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusReceiver _serviceBusReceiver;

        public NewsProcessorServiceIntegrationTests()
        {
            // Load configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            // Set up ServiceCollection for dependency injection
            var services = new ServiceCollection();

            services.AddSingleton(_configuration);
            services.AddHttpClient();
            services.AddLogging(builder => builder.AddConsole());

            // Set up Service Bus client and receiver
            var serviceBusConnection = _configuration["ServiceBusConnection"];
            var queueName = _configuration["ServiceBus:QueueName"];
            _serviceBusClient = new ServiceBusClient(serviceBusConnection);
            _serviceBusReceiver = _serviceBusClient.CreateReceiver(queueName);

            services.AddSingleton(sp => _serviceBusClient.CreateSender(queueName));

            services.AddSingleton<INewsProcessorService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var logger = sp.GetRequiredService<ILogger<NewsProcessorService>>();
                var apiBaseUrl = _configuration["WebApiBaseUrl"];
                var serviceBusSender = sp.GetRequiredService<ServiceBusSender>();

                return new NewsProcessorService(
                    httpClientFactory.CreateClient(),
                    logger,
                    apiBaseUrl,
                    serviceBusSender);
            });

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ProcessAndSendNewsItemsAsync_ShouldSendUpToFiveMessagesToServiceBus()
        {
            // Arrange
            var newsProcessorService = _serviceProvider.GetRequiredService<INewsProcessorService>();

            // Act
            await newsProcessorService.ProcessAndSendNewsItemsAsync();

            // Assert
            var receivedMessages = new List<ServiceBusReceivedMessage>();
            for (int i = 0; i < 5; i++)
            {
                var message = await _serviceBusReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
                if (message == null) break;
                receivedMessages.Add(message);
            }

            Assert.InRange(receivedMessages.Count, 1, 5);

            foreach (var receivedMessage in receivedMessages)
            {
                var receivedNewsItem = JsonSerializer.Deserialize<NewsItem>(receivedMessage.Body.ToString());
                Assert.NotNull(receivedNewsItem);
                Assert.NotEmpty(receivedNewsItem.id);
                Assert.NotEmpty(receivedNewsItem.Title);
                Assert.NotEmpty(receivedNewsItem.Snippet);
            }
        }

        public void Dispose()
        {
            _serviceBusReceiver.DisposeAsync().AsTask().Wait();
            _serviceBusClient.DisposeAsync().AsTask().Wait();
        }
    }
}