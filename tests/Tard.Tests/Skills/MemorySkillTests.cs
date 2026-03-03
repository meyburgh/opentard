using System.Text.Json;
using Moq;
using Tard.Memory;
using Tard.Skills;

namespace Tard.Tests.Skills;

public class MemorySkillTests
{
    private readonly Mock<IMemoryStore> _store = new();
    private readonly MemorySkill _skill;
    private readonly SkillContext _context = new("user1");

    public MemorySkillTests()
    {
        _skill = new MemorySkill(_store.Object);
    }

    [Fact]
    public void HasCorrectMetadata()
    {
        Assert.Equal("memory", _skill.Name);
        Assert.NotEmpty(_skill.Description);
    }

    [Fact]
    public async Task Save_CallsStore()
    {
        var args = JsonDocument.Parse("""{"action":"save","key":"name","value":"Alice"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        _store.Verify(s => s.SaveAsync("user1", "name", "Alice", It.IsAny<CancellationToken>()), Times.Once);
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Recall_Found_ReturnsValue()
    {
        _store.Setup(s => s.RecallAsync("user1", "name", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Alice");

        var args = JsonDocument.Parse("""{"action":"recall","key":"name"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal("Alice", json.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Recall_NotFound_ReturnsFalse()
    {
        _store.Setup(s => s.RecallAsync("user1", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var args = JsonDocument.Parse("""{"action":"recall","key":"missing"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task List_ReturnsMemories()
    {
        _store.Setup(s => s.ListAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        var args = JsonDocument.Parse("""{"action":"list"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        var json = JsonDocument.Parse(result);
        Assert.Equal(2, json.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Delete_CallsStore()
    {
        var args = JsonDocument.Parse("""{"action":"delete","key":"name"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        _store.Verify(s => s.DeleteAsync("user1", "name", It.IsAny<CancellationToken>()), Times.Once);
        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"action":"invalid"}""").RootElement;
        var result = await _skill.ExecuteAsync(args, _context);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }
}
