using FluentAssertions;
using HybridOutbox.Abstractions;
using HybridOutbox.DynamoDb.Configuration;
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
    public void AddDynamoDb_RegistersIDynamoDbStore()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IDynamoDbStore));
    }

    [Fact]
    public void AddDynamoDb_RegistersIOutboxStore()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IOutboxStore));
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
            opts.TableName = "CustomTable";
            opts.RetentionPeriod = TimeSpan.FromDays(30);
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DynamoDbOutboxOptions>>().Value;

        options.TableName.Should().Be("CustomTable");
        options.RetentionPeriod.Should().Be(TimeSpan.FromDays(30));
    }
}
