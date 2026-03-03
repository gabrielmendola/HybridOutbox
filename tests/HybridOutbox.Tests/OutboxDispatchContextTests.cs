using FluentAssertions;
using Xunit;

namespace HybridOutbox.Tests;

public sealed class OutboxDispatchContextTests
{
    [Fact]
    public void IsOutboxDispatching_IsFalseByDefault()
    {
        var context = new OutboxDispatchContext();

        context.IsOutboxDispatching.Should().BeFalse();
    }

    [Fact]
    public void BeginDispatch_SetsIsDispatchingToTrue()
    {
        var context = new OutboxDispatchContext();

        using (context.BeginDispatch())
        {
            context.IsOutboxDispatching.Should().BeTrue();
        }
    }

    [Fact]
    public void DisposingScope_ResetsIsDispatchingToFalse()
    {
        var context = new OutboxDispatchContext();

        var scope = context.BeginDispatch();
        scope.Dispose();

        context.IsOutboxDispatching.Should().BeFalse();
    }

    [Fact]
    public void NestedBeginDispatch_RemainsTrue_AfterInnerScopeDisposed()
    {
        var context = new OutboxDispatchContext();

        using (context.BeginDispatch())
        {
            using (context.BeginDispatch())
            {
                context.IsOutboxDispatching.Should().BeTrue();
            }

        }
    }

    [Fact]
    public async Task IsOutboxDispatching_IsFalse_InSeparateAsyncContext()
    {
        var context = new OutboxDispatchContext();
        bool? valueInOtherContext = null;

        using (context.BeginDispatch())
        {
            context.IsOutboxDispatching.Should().BeTrue();
        }

        await Task.Run(() => { valueInOtherContext = context.IsOutboxDispatching; });

        valueInOtherContext.Should().BeFalse();
    }

    [Fact]
    public async Task BeginDispatch_InOneTask_DoesNotAffectIndependentTask()
    {
        var context = new OutboxDispatchContext();
        bool? valueInIndependentTask = null;

        var independentTask = Task.Run(async () =>
        {
            await Task.Delay(20);
            valueInIndependentTask = context.IsOutboxDispatching;
        });

        using (context.BeginDispatch())
        {
            context.IsOutboxDispatching.Should().BeTrue();
            await independentTask;
        }

        valueInIndependentTask.Should().BeFalse();
    }
}
