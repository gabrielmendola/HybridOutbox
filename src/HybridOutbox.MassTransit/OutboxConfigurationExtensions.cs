using HybridOutbox.Abstractions;
using HybridOutbox.MassTransit.Configuration;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace HybridOutbox.MassTransit;

public static class OutboxConfigurationExtensions
{
    public static void AddHybridOutbox(
        this IBusRegistrationConfigurator configurator)
    {
        var hybridOutboxConfigurator = new OutboxConfigurator(configurator);
        hybridOutboxConfigurator.Configure();
    }

    public static void UseHybridOutbox(
        this IReceiveEndpointConfigurator configurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(context);

        var outboxObserver = new OutboxConsumePipeSpecificationObserver(context);

        configurator.ConnectConsumerConfigurationObserver(outboxObserver);
        configurator.ConnectSagaConfigurationObserver(outboxObserver);
    }
}