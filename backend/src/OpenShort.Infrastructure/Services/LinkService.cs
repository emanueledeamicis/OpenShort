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
        // Slug Logic
        if (!string.IsNullOrEmpty(link.Slug))
        {
            if (await _context.Links.AnyAsync(l => l.Slug == link.Slug && l.Domain == link.Domain))
            {
                return null; // Slug Conflict
            }
        }
        else
        {
            // Auto-generate
            link.Slug = _slugGenerator.GenerateSlug();
            while (await _context.Links.AnyAsync(l => l.Slug == link.Slug && l.Domain == link.Domain))
            {
                link.Slug = _slugGenerator.GenerateSlug();
            }
        }

        link.CreatedAt = DateTime.UtcNow;
        link.IsActive = true; // Default

        _context.Links.Add(link);
        await _context.SaveChangesAsync();
        return link;
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
