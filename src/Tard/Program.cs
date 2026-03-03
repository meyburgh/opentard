using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Tard.Agent;
using Tard.Ai;
using Tard.Configuration;
using Tard.Memory;
using Tard.Messaging;
using Tard.Skills;
using Tard.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Configuration from environment variables (TARD__OTWAPURL, TARD__ANTHROPICAPIKEY, etc.)
builder.Services.Configure<TardOptions>(
    builder.Configuration.GetSection(TardOptions.SectionName));

// Claude API HttpClient
builder.Services.AddHttpClient<IAiClient, ClaudeAiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<TardOptions>>().Value;
    client.BaseAddress = new Uri("https://api.anthropic.com/");
    client.DefaultRequestHeaders.Add("x-api-key", options.AnthropicApiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
});

// ot-wap gateway (MCP client)
builder.Services.AddSingleton<IMessageGateway, OtWapGateway>();

// Memory store
builder.Services.AddSingleton<IMemoryStore, JsonFileMemoryStore>();

// Skills
builder.Services.AddSingleton<ISkill, TimeSkill>();
builder.Services.AddSingleton<ISkill, ShellSkill>();
builder.Services.AddSingleton<ISkill, MemorySkill>();
builder.Services.AddSingleton<SkillRegistry>();

// Agent
builder.Services.AddSingleton<ITardAgent, TardAgent>();

// Polling worker
builder.Services.AddHostedService<MessagePollingWorker>();

var host = builder.Build();
host.Run();
