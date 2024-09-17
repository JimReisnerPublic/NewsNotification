using System.Text.Json;
using System.Text.Json.Serialization;
using NewsNotifier.DataLayer;

namespace NewsNotifier.RestService
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<Subscription>))]
    [JsonSerializable(typeof(Subscription))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(TestEnvironmentResponse))]
    [JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(NewsNotifier.DataLayer.NewsItem))]
    [JsonSerializable(typeof(List<NewsNotifier.DataLayer.NewsItem>))]
    [JsonSerializable(typeof(List<NewsNotifier.DataLayer.NewsItemAndSubscriber>))]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}