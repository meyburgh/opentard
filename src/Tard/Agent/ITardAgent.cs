using Tard.Messaging;

namespace Tard.Agent;

public interface ITardAgent
{
    Task<string> ProcessMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
}
