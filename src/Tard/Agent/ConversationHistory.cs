using Tard.Ai;

namespace Tard.Agent;

public class ConversationHistory
{
    private readonly List<AiMessage> _messages = new();
    private readonly int _maxMessages;

    public ConversationHistory(int maxMessages = 50)
    {
        _maxMessages = maxMessages;
    }

    public IReadOnlyList<AiMessage> Messages => _messages;

    public void Add(AiMessage message)
    {
        _messages.Add(message);
        // Trim from the front, keeping the most recent messages.
        // Always trim in pairs (user+assistant) to keep conversation coherent.
        while (_messages.Count > _maxMessages && _messages.Count >= 2)
        {
            _messages.RemoveAt(0);
            if (_messages.Count > 0)
                _messages.RemoveAt(0);
        }
    }
}
