using MassTransit;
using Sample.WebApi.Data;
using Sample.WebApi.Entities;
using Sample.WebApi.Messages;

namespace Sample.WebApi.Consumers;

public class TestMessageConsumer : IConsumer<TestCommand>
{
    private readonly ILogger<TestMessageConsumer> _logger;
    private readonly UnitOfWork _unitOfWork;

    public TestMessageConsumer(ILogger<TestMessageConsumer> logger, UnitOfWork unitOfWork)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task Consume(ConsumeContext<TestCommand> context)
    {
        var cmd = context.Message;

        _logger.LogInformation(
            "Received TestCommand: Id={Id}, Text={Text}",
            cmd.Id,
            cmd.Text);

        var entity = new TestMessage
        {
            Id = cmd.Id.ToString(),
            Text = cmd.Text,
            Timestamp = cmd.Timestamp,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Add(entity);
        await _unitOfWork.CommitAsync(context.CancellationToken);
    }
}
