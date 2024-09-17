using Microsoft.Extensions.Logging;
using NewsNotifier.DataLayer;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> CallApiAsync(Subscription subscription)
    {
        using var request = new HttpRequestMessage();
        request.RequestUri = new Uri(subscription.Api.BaseAddress);
        request.Headers.Add("Authorization", $"Bearer {subscription.Api.Key}");

        if (subscription.Api.Method.ToUpper() == "POST")
        {
            request.Method = HttpMethod.Post;
            request.Content = new StringContent(subscription.ParamSet, Encoding.UTF8, "application/json");
        }
        else
        {
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri($"{subscription.Api.BaseAddress}?{subscription.ParamSet}");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error calling API for subscription {subscription.SubscriptionId}");
            throw;
        }
    }
}