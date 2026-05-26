namespace SharedKernel.Caching;

public sealed class CacheStamp
{
    public long Value { get; set; }

    public static CacheStamp New() => new()
    {
        Value = DateTimeOffset.UtcNow.Ticks
    };
}
