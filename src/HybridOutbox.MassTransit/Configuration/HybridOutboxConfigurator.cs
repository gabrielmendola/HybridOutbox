using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using MassTransit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HybridOutbox.MassTransit.Configuration;

public class HybridOutboxConfigurator : IHybridOutboxConfigurator
{
    private readonly IBusRegistrationConfigurator _configurator;

    public HybridOutboxConfigurator(IBusRegistrationConfigurator configurator)
    {
        _configurator = configurator;
    }

    public virtual void Configure(Action<IHybridOutboxConfigurator>? configure)
    {;
        configure?.Invoke(this);
        _configurator.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        _configurator.ReplaceScoped<IScopedBusContextProvider<IBus>, OutboxScopedBusContextProvider<IBus>>();
    }
}