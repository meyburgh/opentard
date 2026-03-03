# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**tard** is a minimal OpenClaw clone — an autonomous AI assistant that communicates with users via WhatsApp. It's a C# .NET 8 worker service that polls the sibling `ot-wap` project (WhatsApp MCP bridge) for incoming messages, processes them through Claude AI with tool use, and sends responses back.

## Build & Run Commands

```bash
# Build
dotnet build tard.sln

# Run all tests
dotnet test tard.sln

# Run a single test class
dotnet test tests/Tard.Tests --filter "FullyQualifiedName~TardAgentTests"

# Run a single test
dotnet test tests/Tard.Tests --filter "FullyQualifiedName~ProcessMessage_SimpleTextResponse"

# Run locally (requires env vars — see .env.example)
dotnet run --project src/Tard

# Docker (runs both tard and ot-wap)
docker compose up --build
docker compose down
```

## Architecture

```
┌──────────┐   MCP/HTTP    ┌──────────┐   WhatsApp API   ┌──────────┐
│   tard   │◄─────────────►│  ot-wap  │◄────────────────►│ WhatsApp │
│ (agent)  │  :8080/mcp    │ (bridge) │                   │  Cloud   │
└──────────┘               └──────────┘                   └──────────┘
```

**Message flow:**
1. `MessagePollingWorker` polls ot-wap's `ReceiveAllMessages` MCP tool every 3s
2. New messages dispatched to `TardAgent` per user
3. Agent builds context (system prompt + user memories + conversation history)
4. Calls Claude API with skills as tools
5. Tool-use loop: executes skills, feeds results back to Claude, repeats until text response
6. Sends final response via ot-wap's `SendTextMessage` MCP tool

### Key Components

| Layer | Interface | Implementation | Purpose |
|-------|-----------|----------------|---------|
| Gateway | `IMessageGateway` | `OtWapGateway` | MCP client to ot-wap (send/receive) |
| AI | `IAiClient` | `ClaudeAiClient` | Claude API with tool use |
| Agent | `ITardAgent` | `TardAgent` | Orchestrator: history + AI + skills |
| Skills | `ISkill` | `TimeSkill`, `ShellSkill`, `MemorySkill` | Extensible tool system |
| Memory | `IMemoryStore` | `JsonFileMemoryStore` | Per-user persistent key-value store |
| Worker | `MessagePollingWorker` | — | BackgroundService polling loop |

### Adding a New Skill

Implement `ISkill` and register in `Program.cs`:
```csharp
builder.Services.AddSingleton<ISkill, MyNewSkill>();
```
Skills are auto-collected by `SkillRegistry` and exposed as Claude tools.

## Configuration

All config via environment variables with `TARD__` prefix (double underscore for nested binding):

| Variable | Required | Default |
|----------|----------|---------|
| `TARD__ANTHROPICAPIKEY` | Yes | — |
| `TARD__OTWAPURL` | No | `http://ot-wap:8080` |
| `TARD__ANTHROPICMODEL` | No | `claude-sonnet-4-20250514` |
| `TARD__POLLINGINTERVALMS` | No | `3000` |
| `TARD__MAXHISTORYPERUSER` | No | `50` |
| `TARD__MEMORYSTOREPATH` | No | `/data/memory` |

## Testing Conventions

- xUnit + Moq
- Tests mirror source structure: `src/Tard/Agent/TardAgent.cs` → `tests/Tard.Tests/Agent/TardAgentTests.cs`
- All external dependencies (AI client, memory store, gateway) mocked via interfaces
- `JsonFileMemoryStore` tests use temp directories with cleanup via `IDisposable`

## Dependencies on ot-wap

The sibling project `../ot-wap` is the WhatsApp bridge. tard communicates with it via MCP over HTTP/SSE at `/mcp`. Key MCP tools used: `ReceiveAllMessages`, `SendTextMessage`. The `docker-compose.yml` builds and runs both services together.
