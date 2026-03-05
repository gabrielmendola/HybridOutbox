using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.DynamoDb.Configuration;
using HybridOutbox.DynamoDb.Internals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HybridOutbox.DynamoDb.Tests;

public sealed class DynamoDbConfigurationExtensionsTests
{
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHybridOutbox().AddDynamoDb();
        return services;
    }

    [Fact]
    public void AddDynamoDb_RegistersIOutboxRepository()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IOutboxRepository));
    }

    [Fact]
    public void AddDynamoDb_RegistersIDynamoDbOutboxContext()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IDynamoDbOutboxContext));
    }

    [Fact]
    public void AddDynamoDb_RegistersIOutboxContext()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IOutboxContext));
    }

    [Fact]
    public void AddDynamoDb_RegistersDynamoDbTableInitializer()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(DynamoDbTableInitializer));
    }

    [Fact]
    public void AddDynamoDb_RegistersDynamoDbOutboxOptions()
    {
        var services = BuildServices();

        services.Should().Contain(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericArguments().Any(a => a == typeof(DynamoDbOutboxOptions)));
    }

    [Fact]
    public void AddDynamoDb_WithCustomOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHybridOutbox().AddDynamoDb(opts =>
        {
            opts.Outbox.TableName = "CustomTable";
            opts.Outbox.RetentionPeriod = TimeSpan.FromDays(30);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DynamoDbOutboxOptions>>().Value;

        options.TableName.Should().Be("CustomTable");
        options.RetentionPeriod.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void AddDynamoDb_RegistersInboxServicesByDefault()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IInboxRepository));
    }

    [Fact]
    public void AddDynamoDb_WithDisableInbox_DoesNotRegisterInboxServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHybridOutbox().DisableInbox().AddDynamoDb();

        services.Should().NotContain(d => d.ImplementationType == typeof(DynamoDbInboxRepository));
    }

    [Fact]
    public void AddDynamoDb_WithCustomInboxOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHybridOutbox().AddDynamoDb(opts =>
        {
            opts.Inbox.TableName = "CustomInboxTable";
            opts.Inbox.RetentionPeriod = TimeSpan.FromDays(14);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DynamoDbInboxOptions>>().Value;

        options.TableName.Should().Be("CustomInboxTable");
        options.RetentionPeriod.Should().Be(TimeSpan.FromDays(14));
    }
}