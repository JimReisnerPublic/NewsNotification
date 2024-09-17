using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using NewsNotifier.Service;
using NewsNotifier.RestService;
using Microsoft.AspNetCore.Routing.Constraints;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
//builder.Services.AddHttpClient<IApiService, ApiService>();
builder.Services.AddHttpClient<IApiService, ApiService>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int>("HttpClientSettings:TimeoutSeconds");
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var endpointUri = configuration["CosmosDB:EndpointUri"];
    var primaryKey = configuration["CosmosDB:PrimaryKey"];
    logger.LogInformation($"Initializing CosmosClient with EndpointUri: {endpointUri}");
    var clientOptions = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway
    };
    try
    {
        return new CosmosClient(endpointUri, primaryKey, clientOptions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing CosmosClient");
        throw;
    }
});

builder.Services.AddSingleton<INewsNotifierService>(sp =>
    new NewsNotifierService(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IApiService>(),
        sp.GetRequiredService<CosmosClient>(),
        sp.GetRequiredService<ILogger<NewsNotifierService>>()
    )
);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1"));
}

app.MapGet("/distinctemails", async (INewsNotifierService service) =>
{
    try
    {
        var emails = await service.GetDistinctEmailsAsync();
        if (emails == null || !emails.Any())
        {
            return Results.NotFound("No distinct emails found.");
        }

        return Results.Ok(emails);
    }
    catch (Exception ex)
    {
        // Log the exception or handle it as needed
        Console.WriteLine($"An error occurred: {ex.Message}");
        return Results.Problem("An error occurred while retrieving distinct emails.");
    }
});

app.MapGet("/subscriptionsperemail/{emailid}", async (string emailid, INewsNotifierService service) =>
{
    var subscription = await service.GetSubscriptionsPerEmailAsync(emailid);
    if (subscription == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(subscription);
});

app.MapGet("/subscription/{id}", async (string id, INewsNotifierService service) =>
{
    var subscription = await service.GetSubscriptionAsync(id);
    if (subscription == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(subscription);
});

app.MapGet("/news-from-subscription/{id}", async (string id, INewsNotifierService service) =>
{
    try
    {
        var result = await service.NewsFromSubscription(id);
        return Results.Ok(result);
    }
    catch (ArgumentException)
    {
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/process-all-subscriptions", async (INewsNotifierService service) =>
{
    try
    {
        var result = await service.ProcessAllSubscriptionsAsync();
        return Results.Ok(JsonSerializer.Serialize(result));
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

//app.MapGet("/test-env", (IWebHostEnvironment env, IConfiguration configuration) =>
//{
//    bool isRunningInAzureContainerApps;
//    string runningEnvironment;

//    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_APP_NAME")))
//    {
//        isRunningInAzureContainerApps = true;
//        runningEnvironment = "Azure Container Apps";
//    }
//    else
//    {
//        isRunningInAzureContainerApps = false;
//        runningEnvironment = "Local Environment";
//    }

//    var envVars = new Dictionary<string, string>
//    {
//        { "Environment", env.EnvironmentName },
//        { "RunningIn", runningEnvironment },
//        { "IsRunningInAzureContainerApps", isRunningInAzureContainerApps.ToString() },
//        { "CosmosDB:EndpointUri", configuration["CosmosDB:EndpointUri"] },
//        { "CosmosDB:PrimaryKey", "**********" }, // Masked for security
//        { "CosmosDB:DatabaseId", configuration["CosmosDB:DatabaseId"] },
//        { "CosmosDB:ContainerId", configuration["CosmosDB:ContainerId"] }
//    };

//    var response = new TestEnvironmentResponse
//    {
//        IsRunningInAzureContainerApps = isRunningInAzureContainerApps,
//        RunningEnvironment = runningEnvironment,
//        Environment = env.EnvironmentName,
//        EnvironmentVariables = envVars
//    };

//    return Results.Ok(response);
//});

app.Run();
