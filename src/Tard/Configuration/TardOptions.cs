namespace Tard.Configuration;

public class TardOptions
{
    public const string SectionName = "Tard";

    public string OtWapUrl { get; set; } = "http://ot-wap:8080";
    public string AnthropicApiKey { get; set; } = "";
    public string AnthropicModel { get; set; } = "claude-sonnet-4-20250514";
    public int PollingIntervalMs { get; set; } = 3000;
    public int MaxHistoryPerUser { get; set; } = 50;
    public string MemoryStorePath { get; set; } = "/data/memory";
    public string SystemPrompt { get; set; } =
        "You are tard, a helpful personal AI assistant. You communicate via WhatsApp. " +
        "Be concise and helpful. You can run shell commands, check the time, and remember things for the user. " +
        "When the user asks you to remember something, use the save_memory tool. " +
        "When they ask you to recall something, use the recall_memory tool.";
}
