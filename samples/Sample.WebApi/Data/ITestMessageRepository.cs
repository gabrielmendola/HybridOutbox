using Sample.WebApi.Entities;

namespace Sample.WebApi.Data;

public interface ITestMessageRepository
{
    Task SaveAsync(TestMessage message, CancellationToken cancellationToken = default);
    Task<TestMessage?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<TestMessage>> GetAllAsync(CancellationToken cancellationToken = default);
}