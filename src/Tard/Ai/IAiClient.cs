using System.Text.Json;

namespace Tard.Ai;

public interface IAiClient
{
    Task<AiResponse> ChatAsync(
        string systemPrompt,
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AiTool>? tools = null,
        CancellationToken cancellationToken = default);
}

public record AiMessage(string Role, IReadOnlyList<AiContentBlock> Content);

public record AiContentBlock
{
    public string Type { get; init; } = "text";
    public string? Text { get; init; }
    public string? ToolUseId { get; init; }
    public string? ToolName { get; init; }
    public JsonElement? ToolInput { get; init; }
    public string? ToolResultContent { get; init; }
}

public record AiTool(string Name, string Description, JsonElement InputSchema);

public record AiResponse
{
    public required string StopReason { get; init; }
    public required IReadOnlyList<AiContentBlock> Content { get; init; }
}
