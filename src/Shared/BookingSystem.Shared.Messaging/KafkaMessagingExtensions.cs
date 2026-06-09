using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookingSystem.Shared.Messaging;

public static class KafkaMessagingExtensions
{
    /// <summary>
    /// Registers <see cref="KafkaServerSettings"/> from the "Kafka" config section,
    /// with the Aspire-injected connection string taking precedence when present.
    /// Call this from any service that reads Kafka settings (producers and consumers).
    /// </summary>
    public static IServiceCollection AddKafkaSettings(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaServerSettings>(
            configuration.GetSection(KafkaServerSettings.SectionName));

        // Aspire injects Kafka as ConnectionStrings:kafka — let it override appsettings.
        if (configuration.GetConnectionString("kafka") is { } aspireKafkaConnStr && string.IsNullOrEmpty(aspireKafkaConnStr) == false)
        {
            services.PostConfigure<KafkaServerSettings>(settings =>
            {
                settings.BootstrapServers = aspireKafkaConnStr;
            });
        }

        return services;
    }

    /// <summary>
    /// Registers <see cref="KafkaServerSettings"/> and the <see cref="IEventPublisher"/> singleton.
    /// Call this from producer services.
    /// </summary>
    public static IServiceCollection AddKafkaMessaging(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKafkaSettings(configuration);

        services.AddSingleton<IEventPublisher>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<KafkaServerSettings>>().Value;
            var logger = sp.GetRequiredService<ILogger<KafkaEventPublisher>>();
            return new KafkaEventPublisher(settings.BootstrapServers, logger);
        });

        return services;
    }
}
