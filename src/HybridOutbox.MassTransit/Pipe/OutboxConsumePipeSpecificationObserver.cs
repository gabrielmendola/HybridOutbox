using MassTransit;
using MassTransit.Configuration;
using MassTransit.Courier.Contracts;
using MassTransit.DependencyInjection;

namespace HybridOutbox.MassTransit.Pipe;

public class OutboxConsumePipeSpecificationObserver :
    IConsumerConfigurationObserver,
    ISagaConfigurationObserver,
    IActivityConfigurationObserver
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ISetScopedConsumeContext _setter;

    public OutboxConsumePipeSpecificationObserver(IRegistrationContext context)
    {
        _serviceProvider = context;
        _setter = context as ISetScopedConsumeContext;
    }

    public void ActivityConfigured<TActivity, TArguments>(
        IExecuteActivityConfigurator<TActivity, TArguments> configurator,
        Uri compensateAddress)
        where TActivity : class, IExecuteActivity<TArguments>
        where TArguments : class
    {
        configurator.RoutingSlip(AddScopedFilter);
    }

    public void ExecuteActivityConfigured<TActivity, TArguments>(
        IExecuteActivityConfigurator<TActivity, TArguments> configurator)
        where TActivity : class, IExecuteActivity<TArguments>
        where TArguments : class
    {
        configurator.RoutingSlip(AddScopedFilter);
    }

    public void CompensateActivityConfigured<TActivity, TLog>(
        ICompensateActivityConfigurator<TActivity, TLog> configurator)
        where TActivity : class, ICompensateActivity<TLog>
        where TLog : class
    {
        configurator.RoutingSlip(AddScopedFilter);
    }

    public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
        where TConsumer : class
    {
    }

    public void ConsumerMessageConfigured<TConsumer, TMessage>(
        IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
        where TConsumer : class
        where TMessage : class
    {
        if (configurator is not IConsumerMessageConfigurator<TMessage> messageConfigurator)
            throw new ConfigurationException(
                $"The scoped filter could not be added: {TypeCache<TConsumer>.ShortName} - {TypeCache<TMessage>.ShortName}");

        AddScopedFilter(messageConfigurator, typeof(TConsumer).FullName!);
    }

    public void SagaConfigured<TSaga>(ISagaConfigurator<TSaga> configurator)
        where TSaga : class, ISaga
    {
    }

    public void StateMachineSagaConfigured<TInstance>(
        ISagaConfigurator<TInstance> configurator,
        SagaStateMachine<TInstance> stateMachine)
        where TInstance : class, ISaga, SagaStateMachineInstance
    {
    }

    public void SagaMessageConfigured<TSaga, TMessage>(ISagaMessageConfigurator<TSaga, TMessage> configurator)
        where TSaga : class, ISaga
        where TMessage : class
    {
        if (configurator is not ISagaMessageConfigurator<TMessage> messageConfigurator)
            throw new ConfigurationException(
                $"The scoped filter could not be added: {TypeCache<TSaga>.ShortName} - {TypeCache<TMessage>.ShortName}");

        AddScopedFilter(messageConfigurator, typeof(TSaga).FullName!);
    }

    private void AddScopedFilter<TMessage>(IPipeConfigurator<ConsumeContext<TMessage>> configurator)
        where TMessage : class
    {
        AddScopedFilter(configurator, string.Empty);
    }

    private void AddScopedFilter<TMessage>(IPipeConfigurator<ConsumeContext<TMessage>> configurator,
        string consumerType)
        where TMessage : class
    {
        var scopeProvider = new ConsumeScopeProvider(_serviceProvider, _setter);
        var filter = new OutboxConsumeFilter<TMessage>(scopeProvider, consumerType);
        var specification = new FilterPipeSpecification<ConsumeContext<TMessage>>(filter);

        configurator.AddPipeSpecification(specification);
    }
}