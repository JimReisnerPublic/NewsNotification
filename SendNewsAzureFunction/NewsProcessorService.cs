using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using NewsNotifier.DataLayer;

namespace NewsNotifier.Service
{
    public interface INewsProcessorService
    {
        Task ProcessAndSendNewsItemsAsync();
    }

    public class NewsProcessorService : INewsProcessorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsProcessorService> _logger;
        private readonly string _apiBaseUrl;
        private readonly ServiceBusSender _serviceBusSender;

        public NewsProcessorService(
            HttpClient httpClient,
            ILogger<NewsProcessorService> logger,
            string apiBaseUrl,
            ServiceBusSender serviceBusSender)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiBaseUrl = apiBaseUrl;
            _serviceBusSender = serviceBusSender;
        }

        public async Task ProcessAndSendNewsItemsAsync()
        {
            try
            {
                var newsItemsAndSubscribers
                    = await GetProcessedNewsItemsAsync();
                await SendMessagesToServiceBusAsync(newsItemsAndSubscribers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing and sending news items");
                throw;
            }
        }

        private async Task<List<NewsItemAndSubscriber>> GetProcessedNewsItemsAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_apiBaseUrl}/process-all-subscriptions", null);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new List<NewsItemAndSubscriber>();
                }

                // First, deserialize the outer JSON string
                var jsonString = JsonSerializer.Deserialize<string>(content);

                // Then, deserialize the inner JSON array
                var allItems = JsonSerializer.Deserialize<List<NewsItemAndSubscriber>>(jsonString,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return allItems?.ToList() ?? new List<NewsItemAndSubscriber>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscriptions");
                throw;
            }
        }

        private async Task SendMessagesToServiceBusAsync(List<NewsItemAndSubscriber> newsItems)
        {
            try
            {
                var messages = newsItems.Select(item => new ServiceBusMessage(JsonSerializer.Serialize(item)));
                await _serviceBusSender.SendMessagesAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending messages to Service Bus");
                throw;
            }
        }
    }
}