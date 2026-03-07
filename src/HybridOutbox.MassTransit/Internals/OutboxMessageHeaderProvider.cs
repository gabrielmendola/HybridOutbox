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

        foreach (var header in _massTransitMessage.Headers)
            switch (header.Key)
            {
                case MessageHeaders.MessageId:
                case MessageHeaders.ContentType:
                    continue;

                default:
                    yield return new KeyValuePair<string, object>(header.Key, header.Value);
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

        _massTransitMessage.Headers.TryGetValue(key, out var headerValue);
        if (headerValue != null)
        {
            value = headerValue;
            return true;
        }

        value = null;
        return false;
    }
}