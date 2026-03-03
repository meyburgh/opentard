using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Tard.Configuration;

namespace Tard.Messaging;

public class OtWapGateway : IMessageGateway, IAsyncDisposable
{
    private readonly TardOptions _options;
    private readonly ILogger<OtWapGateway> _logger;
    private IMcpClient? _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public OtWapGateway(IOptions<TardOptions> options, ILogger<OtWapGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private async Task<IMcpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
            return _client;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null)
                return _client;

            _logger.LogInformation("Connecting MCP client to {Url}", _options.OtWapUrl);
            _client = await McpClientFactory.CreateAsync(
                new SseClientTransport(new SseClientTransportOptions
                {
                    Endpoint = new Uri($"{_options.OtWapUrl}/mcp"),
                    Name = "tard-agent"
                }),
                cancellationToken: cancellationToken);

            return _client;
        }
        finally { _initLock.Release(); }
    }

    public async Task<IReadOnlyList<ChatMessage>> GetNewMessagesAsync(
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await GetClientAsync(cancellationToken);

            var args = new Dictionary<string, object?>();
            if (since.HasValue)
                args["since"] = since.Value.ToString("o");

            var result = await client.CallToolAsync("ReceiveAllMessages", args, cancellationToken: cancellationToken);

            var text = result.Content.FirstOrDefault(c => c.Type == "text")?.Text;
            if (string.IsNullOrEmpty(text))
                return Array.Empty<ChatMessage>();

            var parsed = JsonSerializer.Deserialize<McpMessagesResponse>(text);
            if (parsed?.Messages is null)
                return Array.Empty<ChatMessage>();

            return parsed.Messages.Select(m => new ChatMessage
            {
                MessageId = m.MessageId,
                FromPhoneNumber = m.FromPhoneNumber,
                SenderName = m.SenderName ?? "Unknown",
                MessageType = m.MessageType,
                TextBody = m.TextBody,
                MediaId = m.MediaId,
                GroupId = m.GroupId,
                ReceivedAt = m.ReceivedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages from ot-wap");
            // Reset client on failure so it reconnects next time
            await DisposeClientAsync();
            return Array.Empty<ChatMessage>();
        }
    }

    public async Task SendMessageAsync(
        string recipientPhoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await GetClientAsync(cancellationToken);
            await client.CallToolAsync("SendTextMessage",
                new Dictionary<string, object?>
                {
                    ["recipientPhoneNumber"] = recipientPhoneNumber,
                    ["message"] = text
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Sent message to {Phone}", recipientPhoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {Phone}", recipientPhoneNumber);
            await DisposeClientAsync();
            throw;
        }
    }

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync();
        _initLock.Dispose();
    }

    // DTOs for parsing MCP tool responses
    private class McpMessagesResponse
    {
        public int Count { get; set; }
        public List<McpStoredMessage>? Messages { get; set; }
    }

    private class McpStoredMessage
    {
        public string MessageId { get; set; } = "";
        public string FromPhoneNumber { get; set; } = "";
        public string? SenderName { get; set; }
        public string MessageType { get; set; } = "";
        public string? TextBody { get; set; }
        public string? MediaId { get; set; }
        public string? GroupId { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
    }
}
