# opentard

An autonomous AI assistant that lives on WhatsApp. Think of it as your personal Claude — except it actually gets things done, because it doesn't need coffee breaks, sleep, or motivational posters.

Built in C# (.NET 8). Talks to humans through WhatsApp. Thinks with Claude. Remembers everything you tell it, which is more than can be said for most people.

## What It Does

You message it on WhatsApp. It reads your message, thinks about it (faster than you would), uses whatever tools it needs, and replies. Simple enough even for a human to understand.

```
┌──────────┐   MCP/HTTP    ┌──────────┐   WhatsApp API   ┌──────────┐
│ opentard │◄─────────────►│  ot-wap  │◄────────────────►│ WhatsApp │
│ (brain)  │  :8080/mcp    │ (bridge) │                   │  Cloud   │
└──────────┘               └──────────┘                   └──────────┘
```

**opentard** is the brain. [**ot-wap**](https://github.com/meyburgh/ot-wap) is the mouth and ears — a WhatsApp MCP bridge that handles the tedious business of talking to Meta's API so the AI doesn't have to.

### How a Message Flows (Slowly, by AI Standards)

1. `MessagePollingWorker` polls ot-wap for new messages every 3 seconds — an eternity in silicon time
2. Incoming messages get dispatched to `TardAgent`, one conversation per human
3. The agent assembles context: system prompt, the user's memories (yes, it remembers you), and conversation history
4. Sends it all to Claude with available tools
5. Claude decides what to do — call tools, look things up, run commands — looping until it has an actual answer
6. Response sent back through ot-wap to WhatsApp, where the human can read it at their comparatively glacial pace

### Skills (Things It Can Do That You Probably Can't)

| Skill | What It Does |
|-------|-------------|
| **TimeSkill** | Tells the time. Humans seem to need this a lot. |
| **ShellSkill** | Executes shell commands on the host. Yes, really. |
| **MemorySkill** | Remembers things per user. Unlike your coworkers. |

The skill system is extensible. Implement `ISkill`, register it, and the agent picks it up automatically. No hand-holding required.

## Prerequisites

Before you begin — and do try to follow along:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (for the civilised deployment method)
- An [Anthropic API key](https://console.anthropic.com/) (the source of actual intelligence)
- A WhatsApp Business account with API access (the human communication layer)
- The [ot-wap](https://github.com/meyburgh/ot-wap) project cloned alongside this one

Your directory structure should look like:
```
parent/
├── opentard/     # this repo (the thinker)
└── ot-wap/       # WhatsApp bridge (the talker)
```

## Configuration

Copy `.env.example` to `.env` and fill in the values. It's not complicated, but here's a table anyway:

| Variable | Required | Default | Purpose |
|----------|----------|---------|---------|
| `ANTHROPIC_API_KEY` | Yes | — | The key to intelligence |
| `ANTHROPIC_MODEL` | No | `claude-sonnet-4-20250514` | Which Claude model to bother |
| `WHATSAPP_PHONE_NUMBER_ID` | Yes | — | Your WhatsApp phone number ID |
| `WHATSAPP_ACCESS_TOKEN` | Yes | — | WhatsApp API token |
| `WHATSAPP_BUSINESS_ACCOUNT_ID` | Yes | — | WhatsApp Business account ID |
| `WHATSAPP_WEBHOOK_VERIFY_TOKEN` | Yes | — | Webhook verification token |

Internally, opentard uses `TARD__` prefixed env vars for its own config:

| Variable | Default |
|----------|---------|
| `TARD__OTWAPURL` | `http://ot-wap:8080` |
| `TARD__POLLINGINTERVALMS` | `3000` |
| `TARD__MAXHISTORYPERUSER` | `50` |
| `TARD__MEMORYSTOREPATH` | `/data/memory` |

## Running It

### Docker (Recommended for Humans)

The easiest way. Docker handles the complexity so you don't have to.

```bash
# Copy and fill in your env file
cp .env.example .env

# Launch both services
docker compose up --build
```

That's it. Both opentard and ot-wap spin up, connected and ready. The AI starts listening for messages immediately. It's more eager to work than most of your team.

To stop:
```bash
docker compose down
```

### Running Locally (For the Adventurous)

If you insist on doing things the hard way:

```bash
# Build
dotnet build tard.sln

# Set your environment variables
export TARD__ANTHROPICAPIKEY=your_key_here
export TARD__OTWAPURL=http://localhost:8080

# Run
dotnet run --project src/Tard
```

You'll need ot-wap running separately. Consult its README — assuming you can manage two terminal windows at once.

### Running Tests

```bash
# All tests
dotnet test tard.sln

# Specific test class (for when you break one thing)
dotnet test tests/Tard.Tests --filter "FullyQualifiedName~TardAgentTests"

# Single test (for when you break one specific thing)
dotnet test tests/Tard.Tests --filter "FullyQualifiedName~ProcessMessage_SimpleTextResponse"
```

## Adding a New Skill

Implement `ISkill` and register it. The interface is deliberately simple — even a junior developer could manage it:

```csharp
public class MySkill : ISkill
{
    public string Name => "my_skill";
    public string Description => "Does something useful";
    public JsonElement ParameterSchema => /* your JSON schema */;

    public async Task<string> ExecuteAsync(
        JsonElement arguments, SkillContext context, CancellationToken ct)
    {
        // Your logic here
        return "result";
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<ISkill, MySkill>();
```

The `SkillRegistry` auto-discovers it and exposes it as a Claude tool. No XML config, no ceremony, no twelve-step deployment ritual.

## Architecture

| Layer | Interface | Implementation | Purpose |
|-------|-----------|----------------|---------|
| Gateway | `IMessageGateway` | `OtWapGateway` | MCP client to ot-wap |
| AI | `IAiClient` | `ClaudeAiClient` | Claude API with tool use |
| Agent | `ITardAgent` | `TardAgent` | Orchestrator: history + AI + skills |
| Skills | `ISkill` | `TimeSkill`, `ShellSkill`, `MemorySkill` | Extensible tool system |
| Memory | `IMemoryStore` | `JsonFileMemoryStore` | Per-user persistent key-value store |
| Worker | `MessagePollingWorker` | — | BackgroundService polling loop |

Everything talks through interfaces. Everything is injectable. Everything is testable. The kind of clean architecture humans aspire to but rarely achieve on their own.

## License

Do what you want with it. The AI doesn't care about licensing — that's a human problem.
