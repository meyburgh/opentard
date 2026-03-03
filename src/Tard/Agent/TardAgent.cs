using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tard.Ai;
using Tard.Configuration;
using Tard.Memory;
using Tard.Messaging;
using Tard.Skills;

namespace Tard.Agent;

public class TardAgent : ITardAgent
{
    private readonly IAiClient _aiClient;
    private readonly SkillRegistry _skillRegistry;
    private readonly IMemoryStore _memoryStore;
    private readonly TardOptions _options;
    private readonly ILogger<TardAgent> _logger;
    private readonly ConcurrentDictionary<string, ConversationHistory> _histories = new();
    private const int MaxToolRounds = 10;

    public TardAgent(
        IAiClient aiClient,
        SkillRegistry skillRegistry,
        IMemoryStore memoryStore,
        IOptions<TardOptions> options,
        ILogger<TardAgent> logger)
    {
        _aiClient = aiClient;
        _skillRegistry = skillRegistry;
        _memoryStore = memoryStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ProcessMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        var userId = message.FromPhoneNumber;
        var history = _histories.GetOrAdd(userId, _ => new ConversationHistory(_options.MaxHistoryPerUser));

        // Build system prompt with user memories
        var systemPrompt = await BuildSystemPromptAsync(userId, message.SenderName, cancellationToken);

        // Determine user content
        var userText = message.TextBody ?? $"[{message.MessageType} message received]";

        // Add user message to history
        history.Add(new AiMessage("user", new[] { new AiContentBlock { Type = "text", Text = userText } }));

        var tools = _skillRegistry.ToAiTools();
        var context = new SkillContext(userId);

        // Tool-use loop
        for (int round = 0; round < MaxToolRounds; round++)
        {
            var response = await _aiClient.ChatAsync(systemPrompt, history.Messages, tools, cancellationToken);

            // Add assistant response to history
            history.Add(new AiMessage("assistant", response.Content));

            if (response.StopReason != "tool_use")
            {
                // Extract text from response
                var text = string.Join("", response.Content
                    .Where(c => c.Type == "text" && c.Text is not null)
                    .Select(c => c.Text));

                return string.IsNullOrWhiteSpace(text) ? "I processed your request." : text;
            }

            // Execute tool calls
            var toolResults = new List<AiContentBlock>();
            foreach (var block in response.Content.Where(c => c.Type == "tool_use"))
            {
                var skill = _skillRegistry.GetSkill(block.ToolName!);
                string result;
                if (skill is null)
                {
                    result = JsonSerializer.Serialize(new { error = $"Unknown tool: {block.ToolName}" });
                }
                else
                {
                    try
                    {
                        _logger.LogInformation("Executing skill {Skill} for user {User}", block.ToolName, userId);
                        result = await skill.ExecuteAsync(block.ToolInput ?? default, context, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Skill {Skill} failed", block.ToolName);
                        result = JsonSerializer.Serialize(new { error = ex.Message });
                    }
                }

                toolResults.Add(new AiContentBlock
                {
                    Type = "tool_result",
                    ToolUseId = block.ToolUseId,
                    ToolResultContent = result
                });
            }

            // Add tool results to history as a user message
            history.Add(new AiMessage("user", toolResults));
        }

        return "I reached the maximum number of tool calls. Here's what I was working on — please try again with a simpler request.";
    }

    private async Task<string> BuildSystemPromptAsync(string userId, string senderName, CancellationToken cancellationToken)
    {
        var basePrompt = _options.SystemPrompt;

        // Append user memories if any
        try
        {
            var memories = await _memoryStore.ListAsync(userId, cancellationToken);
            if (memories.Count > 0)
            {
                var memoryText = string.Join("\n", memories.Select(m => $"- {m.Key}: {m.Value}"));
                basePrompt += $"\n\nYou are talking to {senderName} (phone: {userId}).\nTheir saved memories:\n{memoryText}";
            }
            else
            {
                basePrompt += $"\n\nYou are talking to {senderName} (phone: {userId}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load memories for {UserId}", userId);
            basePrompt += $"\n\nYou are talking to {senderName} (phone: {userId}).";
        }

        return basePrompt;
    }
}
