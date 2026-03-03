using Microsoft.Extensions.Logging.Abstractions;
using Tard.Memory;

namespace Tard.Tests.Memory;

public class JsonFileMemoryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonFileMemoryStore _store;

    public JsonFileMemoryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"tard_test_{Guid.NewGuid():N}");
        _store = new JsonFileMemoryStore(_tempDir, NullLogger<JsonFileMemoryStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAndRecall_RoundTrips()
    {
        await _store.SaveAsync("user1", "color", "blue");
        var result = await _store.RecallAsync("user1", "color");
        Assert.Equal("blue", result);
    }

    [Fact]
    public async Task Recall_NonExistent_ReturnsNull()
    {
        var result = await _store.RecallAsync("user1", "missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task List_ReturnsAllMemories()
    {
        await _store.SaveAsync("user1", "a", "1");
        await _store.SaveAsync("user1", "b", "2");

        var memories = await _store.ListAsync("user1");
        Assert.Equal(2, memories.Count);
        Assert.Equal("1", memories["a"]);
        Assert.Equal("2", memories["b"]);
    }

    [Fact]
    public async Task Delete_RemovesMemory()
    {
        await _store.SaveAsync("user1", "key", "value");
        await _store.DeleteAsync("user1", "key");

        var result = await _store.RecallAsync("user1", "key");
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_OverwritesExisting()
    {
        await _store.SaveAsync("user1", "key", "old");
        await _store.SaveAsync("user1", "key", "new");

        var result = await _store.RecallAsync("user1", "key");
        Assert.Equal("new", result);
    }

    [Fact]
    public async Task DifferentUsers_IsolatedMemories()
    {
        await _store.SaveAsync("user1", "key", "value1");
        await _store.SaveAsync("user2", "key", "value2");

        Assert.Equal("value1", await _store.RecallAsync("user1", "key"));
        Assert.Equal("value2", await _store.RecallAsync("user2", "key"));
    }

    [Fact]
    public async Task List_EmptyUser_ReturnsEmpty()
    {
        var memories = await _store.ListAsync("nobody");
        Assert.Empty(memories);
    }

    [Fact]
    public async Task Save_PersistsToFile()
    {
        await _store.SaveAsync("+14155550001", "test", "persisted");

        // Verify file was created
        var files = Directory.GetFiles(_tempDir, "*.json");
        Assert.Single(files);

        // Create a new store instance pointing at same dir to verify persistence
        var store2 = new JsonFileMemoryStore(_tempDir, NullLogger<JsonFileMemoryStore>.Instance);
        var result = await store2.RecallAsync("+14155550001", "test");
        Assert.Equal("persisted", result);
    }
}
