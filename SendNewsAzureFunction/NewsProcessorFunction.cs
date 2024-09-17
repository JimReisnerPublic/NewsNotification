using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using NewsNotifier.Service;
using Microsoft.Azure.Functions.Worker;

namespace NewsNotifierFunction
{
    public class NewsProcessorFunction
    {
        private readonly INewsProcessorService _newsProcessorService;
        private readonly ILogger<NewsProcessorFunction> _logger;

        public NewsProcessorFunction(
            INewsProcessorService newsProcessorService,
            ILogger<NewsProcessorFunction> logger)
        {
            _newsProcessorService = newsProcessorService;
            _logger = logger;
        }

        [FunctionName("ProcessNewsSubscriptions")]
        public async Task Run([TimerTrigger("0 0 9,13,15 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"News processor function executed at: {DateTime.Now}");

            try
            {
                await _newsProcessorService.ProcessAndSendNewsItemsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing news subscriptions");
            }
        }
    }
}