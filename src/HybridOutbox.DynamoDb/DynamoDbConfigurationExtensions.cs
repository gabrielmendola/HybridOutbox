using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using HybridOutbox;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HybridOutbox.DynamoDb;

public static class DynamoDbConfigurationExtensions
{
    public static OutboxConfigurator AddDynamoDb(
        this OutboxConfigurator configurator,
        Action<DynamoDbOutboxOptions>? configure = null)
    {
        var earlyOptions = new DynamoDbOutboxOptions();
        configure?.Invoke(earlyOptions);

        var optionsBuilder = configurator.Services
            .AddOptions<DynamoDbOutboxOptions>()
            .BindConfiguration(DynamoDbOutboxOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        configurator.Services.AddSingleton<IOutboxRepository, DynamoDbOutboxRepository>();
        configurator.Services.AddSingleton<IOutboxJobLock, DynamoDbJobLock>();

        configurator.Services.AddScoped<IDynamoDbStore, DynamoDbStore>();
        configurator.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<IDynamoDbStore>());

        configurator.Services.AddSingleton<DynamoDbTableInitializer>();

        return configurator;
    }
}
