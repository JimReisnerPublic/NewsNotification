using NewsNotifier.DataLayer;

namespace NewsNotifier.Service
{
    public interface INewsNotifierService
    {
        Task<Subscription> GetSubscriptionAsync(string id);
        Task<List<Subscription>> GetSubscriptionsPerEmailAsync(string emailId);
        Task<List<NewsItem>> NewsFromSubscription(string subscriptionId);
        Task<List<string>> GetDistinctEmailsAsync();
        Task<List<NewsItemAndSubscriber>> ProcessAllSubscriptionsAsync();
    }
}