using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Caching;

public sealed class SafeCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly ILogger<SafeCacheService> _logger;

    public SafeCacheService(IDistributedCache cache, ILogger<SafeCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct);
            return bytes == null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {CacheKey}. Falling back to source.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            await _cache.SetAsync(
                key,
                bytes,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                },
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {CacheKey}. Continuing without cache.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove failed for key {CacheKey}. Continuing without cache.", key);
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var value = await factory(ct);
        if (value is not null)
            await SetAsync(key, value, ttl, ct);

        return value;
    }
}
