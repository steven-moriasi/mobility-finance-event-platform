using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MobilityFinance.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMobilityEventPublishing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ServiceBusOptions options = configuration
            .GetRequiredSection(ServiceBusOptions.SectionName)
            .Get<ServiceBusOptions>()
            ?? throw new InvalidOperationException(
                "Service Bus configuration is required.");
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Service Bus connection string is required.");
        }

        services
            .AddOptions<ServiceBusOptions>()
            .Bind(configuration.GetRequiredSection(ServiceBusOptions.SectionName))
            .Validate(
                value => !string.IsNullOrWhiteSpace(value.ConnectionString)
                    && !string.IsNullOrWhiteSpace(value.EventsTopic),
                "Service Bus configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(
            provider =>
            {
                ServiceBusOptions configured = provider
                    .GetRequiredService<
                        Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>()
                    .Value;
                return new ServiceBusClient(configured.ConnectionString);
            });
        services.AddSingleton<InMemoryEventOutbox>();
        services.AddHostedService<ServiceBusOutboxDispatcher>();
        return services;
    }
}
