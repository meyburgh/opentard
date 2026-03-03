using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Tard.Agent;
using Tard.Ai;
using Tard.Configuration;
using Tard.Memory;
using Tard.Messaging;
using Tard.Skills;

namespace Tard.Tests.Agent;

public class TardAgentTests
{
    private readonly Mock<IAiClient> _aiClient = new();
    private readonly Mock<IMemoryStore> _memoryStore = new();
    private readonly TardOptions _options = new() { MaxHistoryPerUser = 50 };

    private TardAgent CreateAgent(params ISkill[] skills)
    {
        var registry = new SkillRegistry(skills);
        return new TardAgent(
            _aiClient.Object,
            registry,
            _memoryStore.Object,
            Options.Create(_options),
            NullLogger<TardAgent>.Instance);
    }

    private static ChatMessage MakeMessage(string text, string phone = "+1234567890") => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        FromPhoneNumber = phone,
        SenderName = "Test User",
        MessageType = "text",
        TextBody = text,
        ReceivedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ProcessMessage_SimpleTextResponse()
    {
        _memoryStore.Setup(m => m.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        _aiClient.Setup(c => c.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AiMessage>>(),
                It.IsAny<IReadOnlyList<AiTool>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiResponse
            {
                StopReason = "end_turn",
                Content = new[] { new AiContentBlock { Type = "text", Text = "Hello there!" } }
            });

        var agent = CreateAgent();
        var result = await agent.ProcessMessageAsync(MakeMessage("Hi"));

        Assert.Equal("Hello there!", result);
    }

    [Fact]
    public async Task ProcessMessage_ExecutesToolThenReturnsText()
    {
        _memoryStore.Setup(m => m.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var callCount = 0;
        _aiClient.Setup(c => c.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AiMessage>>(),
                It.IsAny<IReadOnlyList<AiTool>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: Claude wants to use a tool
                    return new AiResponse
                    {
                        StopReason = "tool_use",
                        Content = new[]
                        {
                            new AiContentBlock
                            {
                                Type = "tool_use",
                                ToolUseId = "call_1",
                                ToolName = "get_current_time",
                                ToolInput = JsonDocument.Parse("{}").RootElement
                            }
                        }
                    };
                }
                // Second call: Claude returns final text
                return new AiResponse
                {
                    StopReason = "end_turn",
                    Content = new[] { new AiContentBlock { Type = "text", Text = "The time is now!" } }
                };
            });

        var agent = CreateAgent(new TimeSkill());
        var result = await agent.ProcessMessageAsync(MakeMessage("What time is it?"));

        Assert.Equal("The time is now!", result);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ProcessMessage_UnknownToolReturnsError()
    {
        _memoryStore.Setup(m => m.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        var callCount = 0;
        _aiClient.Setup(c => c.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AiMessage>>(),
                It.IsAny<IReadOnlyList<AiTool>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new AiResponse
                    {
                        StopReason = "tool_use",
                        Content = new[]
                        {
                            new AiContentBlock
                            {
                                Type = "tool_use",
                                ToolUseId = "call_1",
                                ToolName = "nonexistent_tool",
                                ToolInput = JsonDocument.Parse("{}").RootElement
                            }
                        }
                    };
                }
                return new AiResponse
                {
                    StopReason = "end_turn",
                    Content = new[] { new AiContentBlock { Type = "text", Text = "Sorry about that." } }
                };
            });

        var agent = CreateAgent();
        var result = await agent.ProcessMessageAsync(MakeMessage("do something"));

        Assert.Equal("Sorry about that.", result);
    }

    [Fact]
    public async Task ProcessMessage_IncludesMemoriesInSystemPrompt()
    {
        _memoryStore.Setup(m => m.ListAsync("+1234567890", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["name"] = "Alice" });

        string? capturedSystem = null;
        _aiClient.Setup(c => c.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AiMessage>>(),
                It.IsAny<IReadOnlyList<AiTool>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<AiMessage>, IReadOnlyList<AiTool>?, CancellationToken>(
                (sys, _, _, _) => capturedSystem = sys)
            .ReturnsAsync(new AiResponse
            {
                StopReason = "end_turn",
                Content = new[] { new AiContentBlock { Type = "text", Text = "Hi Alice!" } }
            });

        var agent = CreateAgent();
        await agent.ProcessMessageAsync(MakeMessage("Hi"));

        Assert.NotNull(capturedSystem);
        Assert.Contains("name: Alice", capturedSystem);
    }

    [Fact]
    public async Task ProcessMessage_MaintainsConversationHistory()
    {
        _memoryStore.Setup(m => m.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        int messageCount = 0;
        _aiClient.Setup(c => c.ChatAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<AiMessage>>(),
                It.IsAny<IReadOnlyList<AiTool>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<AiMessage>, IReadOnlyList<AiTool>?, CancellationToken>(
                (_, msgs, _, _) => messageCount = msgs.Count)
            .ReturnsAsync(new AiResponse
            {
                StopReason = "end_turn",
                Content = new[] { new AiContentBlock { Type = "text", Text = "ok" } }
            });

        var agent = CreateAgent();
        await agent.ProcessMessageAsync(MakeMessage("First", "+1234567890"));
        // After first: history has user(1) + assistant(1) = 2 messages
        // Second call should have 2 + 1 new user = 3
        await agent.ProcessMessageAsync(MakeMessage("Second", "+1234567890"));

        Assert.Equal(3, messageCount);
    }
}
