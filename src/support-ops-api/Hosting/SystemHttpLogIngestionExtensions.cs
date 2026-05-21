using MassTransit;
using SupportOpsApi.Consumers;

namespace SupportOpsApi.Hosting;

public static class SystemHttpLogIngestionExtensions {
    private const string QueueName = "tc.support-ops-api.system-http-logs";
    private const string AppLogQueueName = "tc.support-ops-api.system-app-logs";
    private const string DbEventQueueName = "tc.support-ops-api.db-event-history";
    private const string LogRequestQueueName = "tc.support-ops-api.log-request";
    private const string ServiceRecoveredQueueName = "tc.support-ops-api.service-recovered";

    public static IServiceCollection AddSystemHttpLogIngestion(this IServiceCollection services, IConfiguration configuration, string serviceName) {
        services.AddMassTransit(x => {
            x.AddConsumer<SystemHttpLogConsumer>();
            x.AddConsumer<SystemAppLogConsumer>();
            x.AddConsumer<DbEventHistoryConsumer>();
            x.AddConsumer<SupportOpsLogRequestConsumer>();
            x.AddConsumer<ServiceRecoveredConsumer>();

            x.UsingRabbitMq((context, cfg) => {
                var (host, port, user, pass) = ResolveRabbitMq(configuration);
                Console.WriteLine($"[Messaging] {serviceName} connecting to RabbitMQ at {host}:{port}");

                cfg.Host(host, port, "/", h => {
                    h.Username(user);
                    h.Password(pass);
                });

                cfg.ReceiveEndpoint(QueueName, e => {
                    e.PrefetchCount = 256;
                    e.ConcurrentMessageLimit = 16;
                    e.UseMessageRetry(r => r.Intervals(200, 500, 1000));
                    e.ConfigureConsumer<SystemHttpLogConsumer>(context);
                });

                cfg.ReceiveEndpoint(AppLogQueueName, e => {
                    e.PrefetchCount = 256;
                    e.ConcurrentMessageLimit = 16;
                    e.UseMessageRetry(r => r.Intervals(200, 500, 1000));
                    e.ConfigureConsumer<SystemAppLogConsumer>(context);
                });

                cfg.ReceiveEndpoint(DbEventQueueName, e => {
                    e.PrefetchCount = 256;
                    e.ConcurrentMessageLimit = 16;
                    e.UseMessageRetry(r => r.Intervals(200, 500, 1000));
                    e.ConfigureConsumer<DbEventHistoryConsumer>(context);
                });

                cfg.ReceiveEndpoint(LogRequestQueueName, e => {
                    e.PrefetchCount = 32;
                    e.ConcurrentMessageLimit = 8;
                    e.UseMessageRetry(r => r.Intervals(200, 500, 1000));
                    e.ConfigureConsumer<SupportOpsLogRequestConsumer>(context);
                });

                cfg.ReceiveEndpoint(ServiceRecoveredQueueName, e => {
                    e.PrefetchCount = 64;
                    e.ConcurrentMessageLimit = 4;
                    e.UseMessageRetry(r => r.Intervals(200, 500, 1000));
                    e.ConfigureConsumer<ServiceRecoveredConsumer>(context);
                });
            });
        });

        return services;
    }

    private static (string Host, ushort Port, string User, string Pass) ResolveRabbitMq(IConfiguration configuration) {
        var defaultHost = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"))
            ? "localhost"
            : "rabbitmq";

        var host = configuration["RABBITMQ__HOST"] ?? configuration["RABBITMQ:HOST"] ?? defaultHost;
        var portValue = configuration["RABBITMQ__PORT"] ?? configuration["RABBITMQ:PORT"] ?? "5672";
        var user = configuration["RABBITMQ__USER"] ?? configuration["RABBITMQ:USER"] ?? "guest";
        var pass = configuration["RABBITMQ__PASSWORD"] ?? configuration["RABBITMQ:PASSWORD"] ?? "guest";

        if (!ushort.TryParse(portValue, out var port)) {
            port = 5672;
        }

        return (host, port, user, pass);
    }
}
