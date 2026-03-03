using System.Text.Json;
using Tard.Skills;

namespace Tard.Tests.Skills;

public class TimeSkillTests
{
    private readonly TimeSkill _skill = new();

    [Fact]
    public void HasCorrectMetadata()
    {
        Assert.Equal("get_current_time", _skill.Name);
        Assert.NotEmpty(_skill.Description);
    }

    [Fact]
    public async Task Execute_ReturnsValidDateTimes()
    {
        var context = new SkillContext("user1");
        var result = await _skill.ExecuteAsync(JsonDocument.Parse("{}").RootElement, context);

        var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("utc", out var utc));
        Assert.True(json.RootElement.TryGetProperty("local", out var local));
        Assert.True(json.RootElement.TryGetProperty("timezone", out _));

        // Verify the times are parseable
        Assert.True(DateTimeOffset.TryParse(utc.GetString(), out _));
        Assert.True(DateTimeOffset.TryParse(local.GetString(), out _));
    }

    [Fact]
    public async Task Execute_UtcTimeIsCloseToNow()
    {
        var before = DateTimeOffset.UtcNow;
        var context = new SkillContext("user1");
        var result = await _skill.ExecuteAsync(JsonDocument.Parse("{}").RootElement, context);
        var after = DateTimeOffset.UtcNow;

        var json = JsonDocument.Parse(result);
        var utc = DateTimeOffset.Parse(json.RootElement.GetProperty("utc").GetString()!);

        Assert.InRange(utc, before, after);
    }
}
