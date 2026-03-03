using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tard.Skills;

public class ShellSkill : ISkill
{
    private readonly ILogger<ShellSkill> _logger;
    private const int TimeoutMs = 30_000;
    private const int MaxOutputLength = 4000;

    public ShellSkill(ILogger<ShellSkill> logger)
    {
        _logger = logger;
    }

    public string Name => "run_shell_command";

    public string Description =>
        "Execute a shell command and return its output. " +
        "Use this for system tasks, checking files, running scripts, etc. " +
        "Commands time out after 30 seconds. Output is truncated to 4000 characters.";

    public JsonElement ParameterSchema { get; } = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "command": {
                    "type": "string",
                    "description": "The shell command to execute"
                }
            },
            "required": ["command"]
        }
        """).RootElement.Clone();

    public async Task<string> ExecuteAsync(JsonElement arguments, SkillContext context, CancellationToken cancellationToken = default)
    {
        var command = arguments.GetProperty("command").GetString()
            ?? throw new ArgumentException("command is required");

        _logger.LogInformation("Executing shell command for user {UserId}: {Command}", context.UserId, command);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeoutMs);

            var isWindows = OperatingSystem.IsWindows();
            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/bash",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start process");

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            var output = string.IsNullOrEmpty(stderr)
                ? stdout
                : $"{stdout}\n[stderr]: {stderr}";

            if (output.Length > MaxOutputLength)
                output = output[..MaxOutputLength] + "\n...[truncated]";

            return JsonSerializer.Serialize(new
            {
                exitCode = process.ExitCode,
                output
            });
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                exitCode = -1,
                output = "Command timed out after 30 seconds."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                exitCode = -1,
                output = $"Error: {ex.Message}"
            });
        }
    }
}
