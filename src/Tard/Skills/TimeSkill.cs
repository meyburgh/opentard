using System.Text.Json;

namespace Tard.Skills;

public class TimeSkill : ISkill
{
    public string Name => "get_current_time";

    public string Description =>
        "Get the current date and time in UTC and local timezone.";

    public JsonElement ParameterSchema { get; } = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {},
            "required": []
        }
        """).RootElement.Clone();

    public Task<string> ExecuteAsync(JsonElement arguments, SkillContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        var result = JsonSerializer.Serialize(new
        {
            utc = DateTimeOffset.UtcNow.ToString("o"),
            local = now.ToString("o"),
            timezone = TimeZoneInfo.Local.DisplayName
        });
        return Task.FromResult(result);
    }
}
