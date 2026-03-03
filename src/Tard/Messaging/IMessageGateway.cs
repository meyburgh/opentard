namespace Tard.Messaging;

public interface IMessageGateway
{
    Task<IReadOnlyList<ChatMessage>> GetNewMessagesAsync(
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);

    Task SendMessageAsync(
        string recipientPhoneNumber,
        string text,
        CancellationToken cancellationToken = default);
}
