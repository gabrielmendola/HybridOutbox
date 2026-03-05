using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HybridOutbox.Configuration;

public sealed class OutboxConfigurator
{
    private readonly OptionsBuilder<OutboxOptions> _optionsBuilder;

    public IServiceCollection Services { get; }
    public bool InboxEnabled { get; private set; } = true;

    internal OutboxConfigurator(IServiceCollection services, OptionsBuilder<OutboxOptions> optionsBuilder)
    {
        Services = services;
        _optionsBuilder = optionsBuilder;
    }

    public OutboxConfigurator DisableInbox()
    {
        InboxEnabled = false;
        _optionsBuilder.Configure(o => o.Inbox.Enabled = false);
        return this;
    }

    public OutboxConfigurator Configure(Action<OutboxOptions> configure)
    {
        _optionsBuilder.Configure(configure);
        return this;
    }
}