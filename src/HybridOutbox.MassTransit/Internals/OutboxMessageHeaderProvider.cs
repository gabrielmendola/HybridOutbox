using System.Diagnostics.CodeAnalysis;
using MassTransit;
using MassTransit.Transports;

namespace HybridOutbox.MassTransit.Internals;

internal class OutboxMessageHeaderProvider : IHeaderProvider
{
    private readonly OutboxMassTransitMessage _massTransitMessage;

    public OutboxMessageHeaderProvider(OutboxMassTransitMessage massTransitMessage)
    {
        _massTransitMessage = massTransitMessage;
    }

    public IEnumerable<KeyValuePair<string, object>> GetAll()
    {
        yield return new KeyValuePair<string, object>(MessageHeaders.MessageId, _massTransitMessage.MessageId);

        if (!string.IsNullOrWhiteSpace(_massTransitMessage.ContentType))
            yield return new KeyValuePair<string, object>(MessageHeaders.ContentType, _massTransitMessage.ContentType!);

        foreach (var (key, value) in _massTransitMessage.Headers)
            switch (key)
            {
                case MessageHeaders.MessageId:
                case MessageHeaders.ContentType:
                    continue;

                default:
                    yield return new KeyValuePair<string, object>(key, value);
                    break;
            }
    }

    public bool TryGetHeader(string key, [NotNullWhen(true)] out object? value)
    {
        if (nameof(_massTransitMessage.MessageId).Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            value = _massTransitMessage.MessageId;
            return true;
        }

        if (MessageHeaders.ContentType.Equals(key, StringComparison.OrdinalIgnoreCase))
        {
            value = _massTransitMessage.ContentType;
            return true;
        }

        var headerValue = _massTransitMessage.Headers.GetValueOrDefault(key);
        if (headerValue != null)
        {
            value = headerValue;
            return true;
        }

        value = null;
        return false;
    }
}