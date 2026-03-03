using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Tard.Skills;

namespace Tard.Tests.Skills;

public class ShellSkillTests
{
    private readonly ShellSkill _skill = new(NullLogger<ShellSkill>.Instance);

    [Fact]
    public void HasCorrectMetadata()
    {
        Assert.Equal("run_shell_command", _skill.Name);
        Assert.NotEmpty(_skill.Description);
    }

    [Fact]
    public async Task Execute_EchoCommand_ReturnsOutput()
    {
        var args = JsonDocument.Parse("""{"command": "echo hello"}""").RootElement;
        var context = new SkillContext("user1");
        var result = await _skill.ExecuteAsync(args, context);

        var json = JsonDocument.Parse(result);
        Assert.Equal(0, json.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Contains("hello", json.RootElement.GetProperty("output").GetString());
    }

    [Fact]
    public async Task Execute_InvalidCommand_ReturnsNonZeroExit()
    {
        // Use a command that will fail
        var args = JsonDocument.Parse("""{"command": "exit 1"}""").RootElement;
        var context = new SkillContext("user1");
        var result = await _skill.ExecuteAsync(args, context);

        var json = JsonDocument.Parse(result);
        // exit 1 should return exitCode 1
        Assert.NotEqual(0, json.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public async Task Execute_MissingCommand_Throws()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var context = new SkillContext("user1");

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _skill.ExecuteAsync(args, context));
    }

    [Fact]
    public void ParameterSchema_HasCommandProperty()
    {
        var schema = _skill.ParameterSchema;
        Assert.True(schema.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("command", out _));
    }
}
