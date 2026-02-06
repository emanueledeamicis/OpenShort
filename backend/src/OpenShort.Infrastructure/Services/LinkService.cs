using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class LinkService : ILinkService
{
    private readonly AppDbContext _context;
    private readonly ISlugGenerator _slugGenerator;

    public LinkService(AppDbContext context, ISlugGenerator slugGenerator)
    {
        _context = context;
        _slugGenerator = slugGenerator;
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
        link.IsActive = true;

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
        throw new InvalidOperationException($"Failed to create link after {maxRetries} attempts due to slug collisions.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // MySQL unique constraint violation code
        return ex.InnerException?.Message.Contains("Duplicate entry") == true ||
               ex.InnerException?.Message.Contains("UNIQUE constraint") == true;
    }


    public async Task<bool> UpdateAsync(Link link)
    {
        _context.Entry(link).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
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
        return true;
    }

    public async Task<bool> ExistsAsync(long id)
    {
        return await _context.Links.AnyAsync(e => e.Id == id);
    }
}
