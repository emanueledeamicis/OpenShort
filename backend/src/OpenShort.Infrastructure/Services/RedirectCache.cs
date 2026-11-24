using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Services;

// The links already resolved by the public redirect, kept in memory for a short time
public class RedirectCache
{
    private const string KeyPrefix = "redirect";

    private readonly IMemoryCache _cache;

    // Every entry is tied to the generation it was stored in: cancelling it removes them all.
    // The memory cache cannot list its keys, so the entries of one domain cannot be removed one by one
    private CancellationTokenSource _generation = new();

    public RedirectCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    // To be read before loading a link from the database, and passed to Set
    public CancellationToken CurrentGeneration => Volatile.Read(ref _generation).Token;

    public bool TryGet(string domain, string slug, out Link? link)
    {
        return _cache.TryGetValue(BuildKey(domain, slug), out link);
    }

    // A link loaded before the cache was emptied is not stored: it may be one of those that had to go
    public void Set(string domain, string slug, Link link, TimeSpan duration, CancellationToken generation)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(duration)
            .AddExpirationToken(new CancellationChangeToken(generation));

        _cache.Set(BuildKey(domain, slug), link, options);
    }

    public void Remove(string domain, string slug)
    {
        _cache.Remove(BuildKey(domain, slug));
    }

    // Removes every link. The old source is not disposed: a request may still hold its token
    public void Clear()
    {
        var previous = Interlocked.Exchange(ref _generation, new CancellationTokenSource());
        previous.Cancel();
    }

    private static string BuildKey(string domain, string slug) => $"{KeyPrefix}_{domain}_{slug}";
}
