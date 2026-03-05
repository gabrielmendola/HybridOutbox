using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace HybridOutbox.DynamoDb;

public static class DynamoDbConfigurationExtensions
{
    public static OutboxConfigurator AddDynamoDb(
        this OutboxConfigurator configurator,
        Action<DynamoDbOptions>? configure = null)
    {
        var outboxBuilder = configurator.Services
            .AddOptions<DynamoDbOutboxOptions>()
            .BindConfiguration(DynamoDbOutboxOptions.SectionName);

        var inboxBuilder = configurator.Services
            .AddOptions<DynamoDbInboxOptions>()
            .BindConfiguration(DynamoDbInboxOptions.SectionName);

        var lockBuilder = configurator.Services
            .AddOptions<DynamoDbLockOptions>()
            .BindConfiguration(DynamoDbLockOptions.SectionName);

        if (configure is not null)
        {
            outboxBuilder.Configure(opts =>
                configure(new DynamoDbOptions(opts, new DynamoDbInboxOptions(), new DynamoDbLockOptions())));
            inboxBuilder.Configure(opts =>
                configure(new DynamoDbOptions(new DynamoDbOutboxOptions(), opts, new DynamoDbLockOptions())));
            lockBuilder.Configure(opts =>
                configure(new DynamoDbOptions(new DynamoDbOutboxOptions(), new DynamoDbInboxOptions(), opts)));
        }

        configurator.Services.AddSingleton<IOutboxRepository, DynamoDbOutboxRepository>();
        configurator.Services.AddSingleton<IOutboxJobLock, DynamoDbJobLock>();

        configurator.Services.AddScoped<IDynamoDbOutboxContext, DynamoDbOutboxContext>();
        configurator.Services.AddScoped<IOutboxContext>(sp => sp.GetRequiredService<IDynamoDbOutboxContext>());

        configurator.Services.AddSingleton<DynamoDbTableInitializer>();

        if (configurator.InboxEnabled) configurator.Services.AddSingleton<IInboxRepository, DynamoDbInboxRepository>();

        return configurator;
    }
}