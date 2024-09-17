using Microsoft.Azure.Cosmos;
using NewsNotifier.DataLayer;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

//TODO: URL Exists per subscription id
//TODO: Max number of stories

namespace NewsNotifier.Service
{
    public class NewsNotifierService : INewsNotifierService
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;
        private Container writeContainer;

        private string databaseId;
        private string containerId;
        private string writeContainerId;
        private string partitionKeyPath;
        private SemaphoreSlim initializationSemaphore = new SemaphoreSlim(1, 1);
        private bool isInitialized = false;
        private readonly IConfiguration _configuration;
        private readonly IApiService _apiService;
        private readonly ILogger<NewsNotifierService> _logger;

        public NewsNotifierService(IConfiguration configuration, IApiService apiService, 
            CosmosClient cosmosClient, ILogger<NewsNotifierService> logger)
        {
            _configuration = configuration;
            _apiService = apiService;
            this.cosmosClient = cosmosClient;
            _logger = logger;
            this.databaseId = _configuration["CosmosDB:DatabaseId"];
            this.containerId = _configuration["CosmosDB:ContainerId"];
            this.writeContainerId = _configuration["CosmosDB:WriteContainerId"];

            _logger.LogInformation($"NewsNotifierService initialized with DatabaseId: {databaseId}, " +
                $"ContainerId: {containerId} WriteContainerId: {writeContainerId}");

        }

        private async Task EnsureInitializedAsync()
        {
            if (!isInitialized)
            {
                await initializationSemaphore.WaitAsync();
                try
                {
                    if (!isInitialized)
                    {
                        await InitializeCosmosAsync();
                        isInitialized = true;
                    }
                }
                finally
                {
                    initializationSemaphore.Release();
                }
            }
        }
        private async Task InitializeCosmosAsync()
        {
            this.database = this.cosmosClient.GetDatabase(databaseId);
            this.container = this.database.GetContainer(containerId);
            this.writeContainer = this.database.GetContainer(writeContainerId);

            var response = await this.container.ReadContainerAsync();
            ContainerProperties containerProperties = response.Resource;
            this.partitionKeyPath = containerProperties.PartitionKeyPath;
        }


        public async Task<Subscription> GetSubscriptionAsync(string id)
        {
            try
            {
                await EnsureInitializedAsync();
                // Remove the leading '/' from the partition key path if present
                string partitionKeyField = this.partitionKeyPath.ToString().TrimStart('/');

                var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", id);

                var queryRequestOptions = new QueryRequestOptions
                {
                    PartitionKey = null // This allows cross-partition query
                };

                var iterator = this.container.GetItemQueryIterator<Subscription>(query, requestOptions: queryRequestOptions);
                var results = await iterator.ReadNextAsync();

                if (results.Count == 0)
                {
                    return null;
                }

                return results.FirstOrDefault();
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Item not found
                return null;
            }
            catch (Exception ex)
            {
                // Handle or log the exception as appropriate for your application
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }

        public async Task<List<string>> GetDistinctEmailsAsync()
        {
            try
            {
                await EnsureInitializedAsync();

                var query = new QueryDefinition(@"
            SELECT DISTINCT c.Subscriber.EmailId 
            FROM c");

                var queryRequestOptions = new QueryRequestOptions
                {
                    PartitionKey = null // This allows cross-partition query
                };

                var emails = new List<string>();
                var iterator = this.container.GetItemQueryIterator<dynamic>(query, requestOptions: queryRequestOptions);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        // Assuming each item has a property 'EmailId'
                        emails.Add(item.EmailId.ToString());
                    }
                }

                return emails;
            }
            catch (CosmosException ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Cosmos DB Error: {ex.Message}. Status Code: {ex.StatusCode}");
                throw;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
        public async Task<List<Subscription>> GetSubscriptionsPerEmailAsync(string emailId)
        {
            try
            {
                _logger.LogInformation($"Fetching subscriptions for email: {emailId}");
                await EnsureInitializedAsync();

                //TODO: pull from config?
                var query = new QueryDefinition(@"
            SELECT 
                c.id as SubscriptionId, 
                c.Subscriber, 
                c.Api, 
                c.ParamSet,
                c.NumberOfStories 
            FROM c 
            WHERE c.Subscriber.EmailId = @emailid")
                    .WithParameter("@emailid", emailId);

                var queryRequestOptions = new QueryRequestOptions
                {
                    PartitionKey = null // This allows cross-partition query
                };

                var subscriptions = new List<Subscription>();
                var iterator = this.container.GetItemQueryIterator<Subscription>(query, requestOptions: queryRequestOptions);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    subscriptions.AddRange(response);
                }
                _logger.LogInformation($"Found {subscriptions.Count} subscriptions for email: {emailId}");
                return subscriptions;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, $"CosmosExcception Error fetching subscriptions for email: {emailId}");
                // Log the exception or handle it as needed
                Console.WriteLine($"Cosmos DB Error: {ex.Message}. Status Code: {ex.StatusCode}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching subscriptions for email: {emailId}");
                // Log the exception or handle it as needed
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }

        public async Task<List<NewsItem>> NewsFromSubscription(string subscriptionId)
        {
            var subscription = await GetSubscriptionAsync(subscriptionId);
            if (subscription == null)
            {
                throw new ArgumentException("Subscription not found", nameof(subscriptionId));
            }

            //string jsonResponse = await _apiService.CallApiAsync(subscription);

            //return DeserializeResponse(subscription.Api.Name, jsonResponse).ToString();
            var apiResponse = await _apiService.CallApiAsync(subscription);
            var newsItems = ParseNewsItems(apiResponse);
            int numberOfStories = int.Parse(subscription.NumberOfStories);
            return newsItems.Take(numberOfStories).ToList();
        }

        private string DeserializeResponse(string apiName, string jsonResponse)
        {
            //I'm deserializing, then serializing because I may want to
            //do transformations in between at some point
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                switch (apiName)
                {

                    default:
                        var newsResponse = System.Text.Json.JsonSerializer.Deserialize<NewsResponse>(jsonResponse, options);
                        return System.Text.Json.JsonSerializer.Serialize(newsResponse, options) ?? throw new Exception("Deserialization returned null for NewsResponse.");
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                Console.WriteLine($"Deserialization failed: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                throw;
            }
        }


        public async Task<List<NewsItemAndSubscriber>> ProcessAllSubscriptionsAsync()
        {
            var allNewsItems = new List<NewsItemAndSubscriber>();
            var distinctEmails = await GetDistinctEmailsAsync();

            foreach (var email in distinctEmails)
            {
                var subscriptions = await GetSubscriptionsPerEmailAsync(email);

                foreach (var subscription in subscriptions)
                {
                    var apiResponse = await _apiService.CallApiAsync(subscription);
                    int numberOfStories = int.Parse(subscription.NumberOfStories);
                    var newsItems = ParseNewsItems(apiResponse).Take(numberOfStories);

                    foreach (var newsItem in newsItems)
                    {
                        NewsItemAndSubscriber newsItemAndSubscriber = new NewsItemAndSubscriber
                        {
                            Subscriber = subscription.Subscriber,
                            NewsItem = newsItem,
                            id = Guid.NewGuid().ToString()
                        };

                        if (!await NewsItemExistsForSubscriberAsync(newsItemAndSubscriber))
                        {
                            newsItemAndSubscriber.NewsItem.id = Guid.NewGuid().ToString();
                            await SaveNewsItemToCosmosDbAsync(newsItemAndSubscriber);
                            allNewsItems.Add(newsItemAndSubscriber);
                        }
                    }
                }
            }

            return allNewsItems;
        }

        private IEnumerable<NewsItem> ParseNewsItems(string apiResponse)
        {
            var newsResponse = JsonSerializer.Deserialize<NewsResponse>(apiResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return newsResponse.Results.Results.News;
        }

        private async Task<bool> NewsItemExistsForSubscriberAsync(NewsItemAndSubscriber newsItemAndSubscriber)
        {
            var query = 
                new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.Subscriber.EmailId = @email AND c.NewsItem.Url = @url")
                .WithParameter("@email", newsItemAndSubscriber.Subscriber.EmailId)
                .WithParameter("@url", newsItemAndSubscriber.NewsItem.Url);

            var iterator = this.writeContainer.GetItemQueryIterator<int>(query);
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }

        private async Task SaveNewsItemToCosmosDbAsync(NewsItemAndSubscriber newsItemAndSubscriber)
        {
            var documentToInsert = new
            {
                id = newsItemAndSubscriber.NewsItem.id ?? Guid.NewGuid().ToString(),
                Subscriber = newsItemAndSubscriber.Subscriber,
                NewsItem = newsItemAndSubscriber.NewsItem
            };

            await this.writeContainer.CreateItemAsync(documentToInsert, new PartitionKey(documentToInsert.id));
        }

    }
}
