using NewsNotifier.DataLayer;

public interface IApiService
{
    Task<string> CallApiAsync(Subscription subscription);
}