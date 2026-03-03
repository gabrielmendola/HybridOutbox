using HybridOutbox.MassTransit.Configuration;
using HybridOutbox.MassTransit.Pipe;
using MassTransit;

namespace HybridOutbox.MassTransit;

public static class OutboxConfigurationExtensions
{
    public static void AddHybridOutbox(
        this IBusRegistrationConfigurator configurator,
        Action<IHybridOutboxConfigurator>? configure = null)
    {
        var hybridOutboxConfigurator = new HybridOutboxConfigurator(configurator);
        hybridOutboxConfigurator.Configure(configure);
    }

    public static void UseHybridOutbox(
        this IReceiveEndpointConfigurator configurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(context);

        var observer = new OutboxConsumePipeSpecificationObserver(context);

        configurator.ConnectConsumerConfigurationObserver(observer);
        configurator.ConnectSagaConfigurationObserver(observer);
    }
}