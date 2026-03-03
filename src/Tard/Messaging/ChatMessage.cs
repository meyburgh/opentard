namespace Tard.Messaging;

public record ChatMessage
{
    public required string MessageId { get; init; }
    public required string FromPhoneNumber { get; init; }
    public required string SenderName { get; init; }
    public required string MessageType { get; init; }
    public string? TextBody { get; init; }
    public string? MediaId { get; init; }
    public string? GroupId { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}
