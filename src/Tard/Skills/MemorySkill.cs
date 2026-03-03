using System.Text.Json;
using Tard.Memory;

namespace Tard.Skills;

public class MemorySkill : ISkill
{
    private readonly IMemoryStore _store;

    public MemorySkill(IMemoryStore store)
    {
        _store = store;
    }

    public string Name => "memory";

    public string Description =>
        "Save, recall, list, or delete persistent memories for the current user. " +
        "Use action='save' with key and value to store something. " +
        "Use action='recall' with key to retrieve it. " +
        "Use action='list' to see all saved memories. " +
        "Use action='delete' with key to remove a memory.";

    public JsonElement ParameterSchema { get; } = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["save", "recall", "list", "delete"],
                    "description": "The memory operation to perform"
                },
                "key": {
                    "type": "string",
                    "description": "The memory key (required for save, recall, delete)"
                },
                "value": {
                    "type": "string",
                    "description": "The value to store (required for save)"
                }
            },
            "required": ["action"]
        }
        """).RootElement.Clone();

    public async Task<string> ExecuteAsync(JsonElement arguments, SkillContext context, CancellationToken cancellationToken = default)
    {
        var action = arguments.GetProperty("action").GetString()
            ?? throw new ArgumentException("action is required");

        switch (action)
        {
            case "save":
            {
                var key = arguments.GetProperty("key").GetString() ?? throw new ArgumentException("key is required for save");
                var value = arguments.GetProperty("value").GetString() ?? throw new ArgumentException("value is required for save");
                await _store.SaveAsync(context.UserId, key, value, cancellationToken);
                return JsonSerializer.Serialize(new { success = true, message = $"Saved memory '{key}'." });
            }
            case "recall":
            {
                var key = arguments.GetProperty("key").GetString() ?? throw new ArgumentException("key is required for recall");
                var value = await _store.RecallAsync(context.UserId, key, cancellationToken);
                return value is not null
                    ? JsonSerializer.Serialize(new { found = true, key, value })
                    : JsonSerializer.Serialize(new { found = false, key, message = $"No memory found for '{key}'." });
            }
            case "list":
            {
                var memories = await _store.ListAsync(context.UserId, cancellationToken);
                return JsonSerializer.Serialize(new { count = memories.Count, memories });
            }
            case "delete":
            {
                var key = arguments.GetProperty("key").GetString() ?? throw new ArgumentException("key is required for delete");
                await _store.DeleteAsync(context.UserId, key, cancellationToken);
                return JsonSerializer.Serialize(new { success = true, message = $"Deleted memory '{key}'." });
            }
            default:
                return JsonSerializer.Serialize(new { error = $"Unknown action: {action}" });
        }
    }
}
