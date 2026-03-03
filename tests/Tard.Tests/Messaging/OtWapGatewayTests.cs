using Tard.Agent;
using Tard.Messaging;

namespace Tard.Tests.Messaging;

public class ChatMessageTests
{
    [Fact]
    public void ChatMessage_PropertiesRoundTrip()
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new ChatMessage
        {
            MessageId = "wamid.123",
            FromPhoneNumber = "+14155550001",
            SenderName = "Alice",
            MessageType = "text",
            TextBody = "Hello",
            ReceivedAt = now
        };

        Assert.Equal("wamid.123", msg.MessageId);
        Assert.Equal("+14155550001", msg.FromPhoneNumber);
        Assert.Equal("Alice", msg.SenderName);
        Assert.Equal("text", msg.MessageType);
        Assert.Equal("Hello", msg.TextBody);
        Assert.Null(msg.MediaId);
        Assert.Null(msg.GroupId);
        Assert.Equal(now, msg.ReceivedAt);
    }

    [Fact]
    public void ChatMessage_MediaMessage()
    {
        var msg = new ChatMessage
        {
            MessageId = "wamid.456",
            FromPhoneNumber = "+14155550001",
            SenderName = "Bob",
            MessageType = "image",
            MediaId = "media_123",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("image", msg.MessageType);
        Assert.Equal("media_123", msg.MediaId);
        Assert.Null(msg.TextBody);
    }
}

public class ConversationHistoryTests
{
    [Fact]
    public void Add_TracksMessages()
    {
        var history = new ConversationHistory(10);
        history.Add(new Ai.AiMessage("user", new[] { new Ai.AiContentBlock { Text = "Hi" } }));

        Assert.Single(history.Messages);
    }

    [Fact]
    public void Add_TrimsWhenOverLimit()
    {
        var history = new ConversationHistory(4);

        for (int i = 0; i < 6; i++)
        {
            history.Add(new Ai.AiMessage(i % 2 == 0 ? "user" : "assistant",
                new[] { new Ai.AiContentBlock { Text = $"msg {i}" } }));
        }

        Assert.True(history.Messages.Count <= 4);
    }

    [Fact]
    public void Messages_MostRecentKept()
    {
        var history = new ConversationHistory(2);

        history.Add(new Ai.AiMessage("user", new[] { new Ai.AiContentBlock { Text = "old" } }));
        history.Add(new Ai.AiMessage("assistant", new[] { new Ai.AiContentBlock { Text = "old reply" } }));
        history.Add(new Ai.AiMessage("user", new[] { new Ai.AiContentBlock { Text = "new" } }));

        // After trimming, the newest messages remain
        var lastMsg = history.Messages[^1];
        Assert.Equal("new", lastMsg.Content[0].Text);
    }
}

public class SkillRegistryTests
{
    [Fact]
    public void GetSkill_ReturnsRegistered()
    {
        var skill = new Tard.Skills.TimeSkill();
        var registry = new Tard.Skills.SkillRegistry(new[] { skill });

        Assert.Same(skill, registry.GetSkill("get_current_time"));
    }

    [Fact]
    public void GetSkill_CaseInsensitive()
    {
        var skill = new Tard.Skills.TimeSkill();
        var registry = new Tard.Skills.SkillRegistry(new[] { skill });

        Assert.Same(skill, registry.GetSkill("GET_CURRENT_TIME"));
    }

    [Fact]
    public void GetSkill_Unknown_ReturnsNull()
    {
        var registry = new Tard.Skills.SkillRegistry(Array.Empty<Tard.Skills.ISkill>());
        Assert.Null(registry.GetSkill("nonexistent"));
    }

    [Fact]
    public void ToAiTools_MapsCorrectly()
    {
        var skill = new Tard.Skills.TimeSkill();
        var registry = new Tard.Skills.SkillRegistry(new[] { skill });

        var tools = registry.ToAiTools();
        Assert.Single(tools);
        Assert.Equal("get_current_time", tools[0].Name);
        Assert.NotEmpty(tools[0].Description);
    }
}
