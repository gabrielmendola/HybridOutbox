using Microsoft.Extensions.DependencyInjection;

namespace HybridOutbox.Configuration;

public sealed class OutboxConfigurator
{
    public IServiceCollection Services { get; }

    internal OutboxConfigurator(IServiceCollection services)
    {
        Services = services;
    }
}