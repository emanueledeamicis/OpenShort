using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using OpenShort.Core.Entities;

namespace OpenShort.Infrastructure.Services;

// The links already resolved by the public redirect, kept in memory for a short time
public class RedirectCache
{
    private const string KeyPrefix = "redirect";

    private readonly IMemoryCache _cache;
    private readonly object _removalLock = new();

    // Every entry is tied to the generation it was stored in: cancelling it removes them all.
    // The memory cache cannot list its keys, so the entries of one domain cannot be removed one by one
    private CancellationTokenSource _generation = new();
    private long _removals;

    public RedirectCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    // The state of the cache at one moment: Set uses it to tell whether something was removed since then
    public readonly record struct Snapshot(CancellationToken Generation, long Removals);

    // To be taken before loading a link from the database, and passed to Set
    public Snapshot TakeSnapshot()
    {
        lock (_removalLock)
        {
            return new Snapshot(_generation.Token, _removals);
        }
    }

    public bool TryGet(string domain, string slug, out Link? link)
    {
        return _cache.TryGetValue(BuildKey(domain, slug), out link);
    }

    // A link loaded before something was removed is not stored: it may be what had to go
    public void Set(string domain, string slug, Link link, TimeSpan duration, Snapshot loadedAt)
    {
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(duration)
            .AddExpirationToken(new CancellationChangeToken(loadedAt.Generation));

        lock (_removalLock)
        {
            if (loadedAt.Removals != _removals)
            {
                return;
            }

            _cache.Set(BuildKey(domain, slug), link, options);
        }
    }

    public void Remove(string domain, string slug)
    {
        lock (_removalLock)
        {
            _removals++;
            _cache.Remove(BuildKey(domain, slug));
        }
    }

    // Removes every link. The old source is not disposed: a request may still hold its token
    public void Clear()
    {
        CancellationTokenSource previous;
        lock (_removalLock)
        {
            previous = _generation;
            _generation = new CancellationTokenSource();
        }

        previous.Cancel();
    }

    // One entry for a link, however the host and the slug are written in the request
    private static string BuildKey(string domain, string slug) => $"{KeyPrefix}_{domain.ToLowerInvariant()}_{slug.ToLowerInvariant()}";
}
