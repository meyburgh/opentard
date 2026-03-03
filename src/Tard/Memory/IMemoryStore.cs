namespace Tard.Memory;

public interface IMemoryStore
{
    Task SaveAsync(string userId, string key, string value, CancellationToken cancellationToken = default);
    Task<string?> RecallAsync(string userId, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string userId, string key, CancellationToken cancellationToken = default);
}
