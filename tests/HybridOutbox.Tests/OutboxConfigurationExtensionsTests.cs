using System.Threading.Channels;
using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.Configuration;
using HybridOutbox.Internals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxConfigurationExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<OutboxOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHybridOutbox(configure);

        services.AddSingleton(NSubstitute.Substitute.For<IOutboxRepository>());
        services.AddSingleton(NSubstitute.Substitute.For<IOutboxDispatcher>());

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddHybridOutbox_RegistersChannel()
    {
        using var provider = BuildProvider();

        var channel = provider.GetService<Channel<OutboxMessage>>();

        channel.Should().NotBeNull();
    }

    [Fact]
    public void AddHybridOutbox_RegistersChannelWriter()
    {
        using var provider = BuildProvider();

        var writer = provider.GetService<ChannelWriter<OutboxMessage>>();

        writer.Should().NotBeNull();
    }

    [Fact]
    public void AddHybridOutbox_RegistersChannelReader()
    {
        using var provider = BuildProvider();

        var reader = provider.GetService<ChannelReader<OutboxMessage>>();

        reader.Should().NotBeNull();
    }

    [Fact]
    public void AddHybridOutbox_RegistersOutboxDispatchContext()
    {
        using var provider = BuildProvider();

        var context = provider.GetService<OutboxDispatchContext>();

        context.Should().NotBeNull();
    }

    [Fact]
    public void AddHybridOutbox_RegistersNoOpJobLock()
    {
        using var provider = BuildProvider();

        var jobLock = provider.GetService<IOutboxJobLock>();

        jobLock.Should().BeOfType<NoOpJobLock>();
    }

    [Fact]
    public void AddHybridOutbox_RegistersOutboxHostedServiceAsHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridOutbox();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(OutboxJob));
    }

    [Fact]
    public void AddHybridOutbox_WithConfigureDelegate_AppliesOptions()
    {
        using var provider = BuildProvider(o =>
        {
            o.Job.Interval = TimeSpan.FromSeconds(42);
            o.Processing.BatchSize = 7;
        });

        var options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;

        options.Job.Interval.Should().Be(TimeSpan.FromSeconds(42));
        options.Processing.BatchSize.Should().Be(7);
    }

    [Fact]
    public void AddHybridOutbox_DefaultOptions_HaveExpectedValues()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;

        options.Job.Interval.Should().Be(TimeSpan.FromSeconds(30));
        options.Processing.Threshold.Should().Be(TimeSpan.FromSeconds(30));
        options.Processing.BatchSize.Should().Be(100);
        options.Processing.Lock.Duration.Should().Be(TimeSpan.FromSeconds(60));
        options.InMemory.DispatchConcurrency.Should().Be(1);
        options.InMemory.Capacity.Should().BeNull();
    }

    [Fact]
    public void AddHybridOutbox_ChannelCapacitySet_CreatesBoundedChannel()
    {
        using var provider = BuildProvider(o => o.InMemory.Capacity = 50);

        var channel = provider.GetRequiredService<Channel<OutboxMessage>>();

        channel.Should().NotBeNull();
        channel.Writer.TryWrite(new OutboxMessage()).Should().BeTrue();
    }

    [Fact]
    public void AddHybridOutbox_ChannelIsSingleton_SameInstanceReturned()
    {
        using var provider = BuildProvider();

        var channel1 = provider.GetRequiredService<Channel<OutboxMessage>>();
        var channel2 = provider.GetRequiredService<Channel<OutboxMessage>>();

        channel1.Should().BeSameAs(channel2);
    }

    [Fact]
    public void AddHybridOutbox_WriterAndReaderDeriveFromSameChannel()
    {
        using var provider = BuildProvider();

        var channel = provider.GetRequiredService<Channel<OutboxMessage>>();
        var writer = provider.GetRequiredService<ChannelWriter<OutboxMessage>>();
        var reader = provider.GetRequiredService<ChannelReader<OutboxMessage>>();

        writer.Should().BeSameAs(channel.Writer);
        reader.Should().BeSameAs(channel.Reader);
    }
}
