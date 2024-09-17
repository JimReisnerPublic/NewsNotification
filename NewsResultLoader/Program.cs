using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace NewsNotifier.DataLayer
{
    public class CosmosDbService
    {
        private readonly IConfiguration _configuration;
        private CosmosClient cosmosClient;
        private Database database;
        private Microsoft.Azure.Cosmos.Container container;

        public CosmosDbService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task InitializeCosmosClientInstanceAsync()
        {
            string endpointUri = _configuration["CosmosDb:EndpointUri"];
            string primaryKey = _configuration["CosmosDb:PrimaryKey"];
            string databaseId = _configuration["CosmosDb:DatabaseId"];
            string containerId = _configuration["CosmosDb:ContainerId"];

            cosmosClient = new CosmosClient(endpointUri, primaryKey);
            database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            container = await database.CreateContainerIfNotExistsAsync(containerId, "/id");
        }

        public async Task SaveNewsItemsAsync(string filePath)
        {
            await InitializeCosmosClientInstanceAsync();

            // Read JSON data from file
            string jsonInput = await File.ReadAllTextAsync(filePath);

            var newsResponse = JsonConvert.DeserializeObject<NewsResponse>(jsonInput);
            var newsItems = newsResponse.Results.Results.News;

            foreach (var newsItem in newsItems)
            {
                try
                {
                    // Ensure the id property is set
                    if (newsItem.id == null)
                    {
                        newsItem.id = Guid.NewGuid().ToString();
                    }

                    await container.CreateItemAsync(newsItem, new PartitionKey(newsItem.id));
                    Console.WriteLine($"Created item in database with id: {newsItem.id}");
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"Item with id: {newsItem.id} already exists");
                }
                catch (CosmosException ex)
                {
                    Console.WriteLine($"Cosmos DB error occurred: {ex.StatusCode} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string filePath = "EndpointCallResult.json";

            var cosmosDbService = new CosmosDbService(configuration);
            await cosmosDbService.SaveNewsItemsAsync(filePath);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

}