using System.Threading.Channels;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;


namespace HybridOutbox;

public static class OutboxConfigurationExtensions
{
    public static OutboxConfigurator AddHybridOutbox(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null)
    {
        var optionsBuilder = services
            .AddOptions<OutboxOptions>()
            .BindConfiguration(OutboxOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OutboxOptions>>().Value;

            return opts.InMemory.Capacity.HasValue
                ? Channel.CreateBounded<OutboxMessage>(new BoundedChannelOptions(opts.InMemory.Capacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                })
                : Channel.CreateUnbounded<OutboxMessage>(new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
        });

        services.TryAddSingleton(sp => sp.GetRequiredService<Channel<OutboxMessage>>().Writer);
        services.TryAddSingleton(sp => sp.GetRequiredService<Channel<OutboxMessage>>().Reader);

        services.TryAddSingleton<OutboxDispatchContext>();
        services.TryAddSingleton<IOutboxJobLock, NoOpJobLock>();

        services.AddHostedService<OutboxInMemory>();
        services.AddHostedService<OutboxJob>();

        return new OutboxConfigurator(services);
    }
}
