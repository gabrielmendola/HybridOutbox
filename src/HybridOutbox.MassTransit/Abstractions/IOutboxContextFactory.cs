using MassTransit;

namespace HybridOutbox.MassTransit.Internals;

public interface IOutboxContextFactory : IProbeSite
{
    Task Send<T>(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next, string consumerType) where T : class;
}