using System.Text.Json;

namespace Tard.Skills;

public interface ISkill
{
    string Name { get; }
    string Description { get; }
    JsonElement ParameterSchema { get; }
    Task<string> ExecuteAsync(JsonElement arguments, SkillContext context, CancellationToken cancellationToken = default);
}

public record SkillContext(string UserId);
