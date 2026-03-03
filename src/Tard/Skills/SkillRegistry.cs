using System.Text.Json;
using Tard.Ai;

namespace Tard.Skills;

public class SkillRegistry
{
    private readonly Dictionary<string, ISkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public SkillRegistry(IEnumerable<ISkill> skills)
    {
        foreach (var skill in skills)
            _skills[skill.Name] = skill;
    }

    public ISkill? GetSkill(string name) =>
        _skills.TryGetValue(name, out var skill) ? skill : null;

    public IReadOnlyList<AiTool> ToAiTools() =>
        _skills.Values.Select(s => new AiTool(s.Name, s.Description, s.ParameterSchema)).ToList();
}
