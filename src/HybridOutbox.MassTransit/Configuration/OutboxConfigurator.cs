using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Internals;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using MassTransit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace HybridOutbox.MassTransit.Configuration;

public sealed class OutboxConfigurator
{
    private readonly IBusRegistrationConfigurator _configurator;

    public OutboxConfigurator(IBusRegistrationConfigurator configurator)
    {
        _configurator = configurator;
    }

    public void Configure()
    {
        _configurator.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();
        _configurator.AddScoped<IOutboxContextFactory, OutboxContextFactory>();
        _configurator.ReplaceScoped<IScopedBusContextProvider<IBus>, OutboxScopedBusContextProvider<IBus>>();
    }
}