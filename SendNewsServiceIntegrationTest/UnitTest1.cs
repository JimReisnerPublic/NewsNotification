using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsNotifier.DataLayer;
using NewsNotifier.Service;
using Xunit;

namespace NewsNotifier.Tests.Integration
{
    public class NewsProcessorServiceIntegrationTests : IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSender _serviceBusSender;
        private readonly ServiceBusReceiver _serviceBusReceiver;
        private readonly ILogger<NewsProcessorService> _logger;

        public NewsProcessorServiceIntegrationTests()
        {
            // Load configuration
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            // Set up real HttpClient
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5); ;

            // Set up Service Bus client, sender, and receiver
            var serviceBusConnection = _configuration["ServiceBusConnection"];
            var queueName = _configuration["ServiceBus:QueueName"];
            _serviceBusClient = new ServiceBusClient(serviceBusConnection);
            _serviceBusSender = _serviceBusClient.CreateSender(queueName);
            _serviceBusReceiver = _serviceBusClient.CreateReceiver(queueName);

            // Set up real Logger (you might want to use a test logger or console logger here)
            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<NewsProcessorService>();
        }

        [Fact]
        public async Task ProcessAndSendNewsItemsAsync_ShouldSendMessagesToServiceBus()
        {
            // Arrange
            var apiBaseUrl = _configuration["WebApiBaseUrl"];
            var newsProcessorService = new NewsProcessorService(
                _httpClient,
                _logger,
                apiBaseUrl,
                _serviceBusSender);

            // Act
            await newsProcessorService.ProcessAndSendNewsItemsAsync();


            //I don't want to read the messages,
            //as I want the logic app to pick them
            //maybe peek?

            // Assert
            //var receivedMessages = new List<ServiceBusReceivedMessage>();
            //while (true)
            //{
            //    var message = await _serviceBusReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
            //    if (message == null) break;
            //    receivedMessages.Add(message);
            //}

            //Assert.NotEmpty(receivedMessages);

            //foreach (var receivedMessage in receivedMessages)
            //{
            //    var receivedNewsItem = JsonSerializer.Deserialize<NewsItem>(receivedMessage.Body.ToString());
            //    Assert.NotNull(receivedNewsItem);
            //    Assert.NotEmpty(receivedNewsItem.id);
            //    Assert.NotEmpty(receivedNewsItem.Title);
            //    Assert.NotEmpty(receivedNewsItem.Snippet);
            //}
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _serviceBusSender.DisposeAsync().AsTask().Wait();
            _serviceBusReceiver.DisposeAsync().AsTask().Wait();
            _serviceBusClient.DisposeAsync().AsTask().Wait();
        }
    }
}