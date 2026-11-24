using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using System.Threading.Channels;

namespace OpenShort.Infrastructure.Services;

public class LinkService : ILinkService
{
    private const string CacheDurationSecondsSettingKey = "CacheDurationSeconds";
    private const string ClickEventChannelFullMessage = "Failed to enqueue click event for {Domain}/{Slug}. Channel might be full.";
    private const string MySqlDuplicateEntryMessage = "Duplicate entry";
    private const string SqliteUniqueConstraintMessage = "UNIQUE constraint";
    private const string LinkCreationCollisionFailureMessage = "Failed to create link after {0} attempts due to slug collisions.";

    private readonly AppDbContext _context;
    private readonly ISlugGenerator _slugGenerator;
    private readonly RedirectCache _redirectCache;
    private readonly ISettingService _settingService;
    private readonly ChannelWriter<ClickEvent> _clickChannel;
    private readonly ILogger<LinkService> _logger;

    public LinkService(
        AppDbContext context, 
        ISlugGenerator slugGenerator, 
        RedirectCache redirectCache,
        ISettingService settingService,
        ChannelWriter<ClickEvent> clickChannel,
        ILogger<LinkService> logger)
    {
        _context = context;
        _slugGenerator = slugGenerator;
        _redirectCache = redirectCache;
        _settingService = settingService;
        _clickChannel = clickChannel;
        _logger = logger;
    }

    public async Task<IEnumerable<Link>> GetAllAsync()
    {
        return await _context.Links.ToListAsync();
    }

    public async Task<Link?> GetByIdAsync(long id)
    {
        return await _context.Links.FindAsync(id);
    }

    public async Task<Link?> GetBySlugAsync(string slug)
    {
        return await _context.Links.FirstOrDefaultAsync(l => l.Slug == slug);
    }

    public async Task<Link?> ResolveAndTrackRedirectAsync(string domain, string slug)
    {
        // 1. Try get from cache
        if (!_redirectCache.TryGet(domain, slug, out var link))
        {
            // Read before the database: a link loaded while the cache is being emptied must not be stored
            var cacheGeneration = _redirectCache.CurrentGeneration;

            // 2. If not in cache, get from DB
            link = await _context.Links
                .FirstOrDefaultAsync(l => l.Slug == slug && l.Domain == domain);

            if (link == null || !IsAvailable(link) || await IsDomainInactiveAsync(domain))
            {
                return null;
            }

            // 3. Save to cache if valid
            var cacheDurationSeconds = await _settingService.GetSettingAsync(CacheDurationSecondsSettingKey, 60);
            if (cacheDurationSeconds > 0)
            {
                _redirectCache.Set(domain, slug, link, TimeSpan.FromSeconds(cacheDurationSeconds), cacheGeneration);
            }
        }

        // A link can expire while it is in the cache
        if (link == null || !IsAvailable(link))
        {
            return null;
        }

        // 4. Track click asynchronously
        var clickEvent = new ClickEvent
        {
            Slug = slug,
            Domain = domain,
            Timestamp = DateTime.UtcNow
        };

        if (!_clickChannel.TryWrite(clickEvent))
        {
            _logger.LogWarning(ClickEventChannelFullMessage, domain, slug);
        }

        return link;
    }

    private static bool IsAvailable(Link link)
    {
        return link.IsActive && (!link.ExpiresAt.HasValue || link.ExpiresAt >= DateTime.UtcNow);
    }

    // A link whose domain is not registered keeps working: only a domain turned off stops its links
    private async Task<bool> IsDomainInactiveAsync(string domain)
    {
        return await _context.Domains.AnyAsync(d => d.Host == domain && !d.IsActive);
    }

    public async Task<Link?> CreateAsync(Link link)
    {
        const int maxRetries = 5;
        
        // If custom slug provided, validate it's unique
        if (!string.IsNullOrEmpty(link.Slug))
        {
            if (await _context.Links.AnyAsync(l => l.Slug == link.Slug && l.Domain == link.Domain))
            {
                return null; // Slug Conflict - don't retry for custom slugs
            }
        }
        else
        {
            // Auto-generate slug
            link.Slug = _slugGenerator.GenerateSlug();
        }

        link.CreatedAt = DateTime.UtcNow;

        // Retry loop for auto-generated slug collisions
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                _context.Links.Add(link);
                await _context.SaveChangesAsync();
                return link;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Detach the entity to allow re-adding with new slug
                _context.Entry(link).State = EntityState.Detached;
                
                // Generate new slug and retry
                link.Slug = _slugGenerator.GenerateSlug();
            }
        }

        // Max retries exceeded
        throw new InvalidOperationException(string.Format(LinkCreationCollisionFailureMessage, maxRetries));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // MySQL unique constraint violation code
        return ex.InnerException?.Message.Contains(MySqlDuplicateEntryMessage) == true ||
               ex.InnerException?.Message.Contains(SqliteUniqueConstraintMessage) == true;
    }


    public async Task<bool> UpdateAsync(Link link)
    {
        _context.Entry(link).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
            
            // Invalidate cache
            _redirectCache.Remove(link.Domain, link.Slug);

            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ExistsAsync(link.Id))
            {
                return false;
            }
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var link = await _context.Links.FindAsync(id);
        if (link == null)
        {
            return false;
        }

        _context.Links.Remove(link);
        await _context.SaveChangesAsync();
        
        // Invalidate cache
        _redirectCache.Remove(link.Domain, link.Slug);

        return true;
    }

    public async Task<bool> ExistsAsync(long id)
    {
        return await _context.Links.AnyAsync(e => e.Id == id);
    }
}
