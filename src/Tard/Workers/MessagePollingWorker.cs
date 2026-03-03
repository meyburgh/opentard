using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tard.Agent;
using Tard.Configuration;
using Tard.Messaging;

namespace Tard.Workers;

public class MessagePollingWorker : BackgroundService
{
    private readonly IMessageGateway _gateway;
    private readonly ITardAgent _agent;
    private readonly TardOptions _options;
    private readonly ILogger<MessagePollingWorker> _logger;
    private readonly HashSet<string> _processedMessageIds = new();
    private DateTimeOffset _lastPollTime = DateTimeOffset.UtcNow;

    public MessagePollingWorker(
        IMessageGateway gateway,
        ITardAgent agent,
        IOptions<TardOptions> options,
        ILogger<MessagePollingWorker> logger)
    {
        _gateway = gateway;
        _agent = agent;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("tard agent starting — polling ot-wap every {Interval}ms", _options.PollingIntervalMs);

        // Small startup delay to let ot-wap come up
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _gateway.GetNewMessagesAsync(_lastPollTime, stoppingToken);

                foreach (var msg in messages)
                {
                    if (!_processedMessageIds.Add(msg.MessageId))
                        continue; // Already processed

                    _logger.LogInformation("New message from {Sender} ({Phone}): {Type}",
                        msg.SenderName, msg.FromPhoneNumber, msg.MessageType);

                    // Skip non-text messages for now (could extend later)
                    if (msg.GroupId is not null)
                    {
                        _logger.LogDebug("Skipping group message {Id}", msg.MessageId);
                        continue;
                    }

                    try
                    {
                        var response = await _agent.ProcessMessageAsync(msg, stoppingToken);
                        await _gateway.SendMessageAsync(msg.FromPhoneNumber, response, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process message {Id} from {Phone}",
                            msg.MessageId, msg.FromPhoneNumber);
                    }
                }

                if (messages.Count > 0)
                    _lastPollTime = messages.Max(m => m.ReceivedAt);

                // Trim processed IDs set to prevent unbounded growth
                if (_processedMessageIds.Count > 10_000)
                    _processedMessageIds.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling loop error");
            }

            await Task.Delay(_options.PollingIntervalMs, stoppingToken);
        }
    }
}
