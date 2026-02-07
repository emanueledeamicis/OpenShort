using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class DomainService : IDomainService
{
    private readonly AppDbContext _context;

    public DomainService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Domain>> GetAllAsync()
    {
        return await _context.Domains.ToListAsync();
    }

    public async Task<Domain?> GetByIdAsync(long id)
    {
        return await _context.Domains.FindAsync(id);
    }

    public async Task<Domain?> GetByHostAsync(string host)
    {
        return await _context.Domains.FirstOrDefaultAsync(d => d.Host == host);
    }

    public async Task<Domain?> CreateAsync(Domain domain)
    {
        if (await _context.Domains.AnyAsync(d => d.Host == domain.Host))
        {
            return null; // Domain already exists
        }

        _context.Domains.Add(domain);
        await _context.SaveChangesAsync();
        return domain;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var domain = await _context.Domains.FindAsync(id);
        if (domain == null)
        {
            return false;
        }

        // Delete all associated links first (cascade delete)
        var associatedLinks = await _context.Links.Where(l => l.Domain == domain.Host).ToListAsync();
        if (associatedLinks.Count > 0)
        {
            _context.Links.RemoveRange(associatedLinks);
        }

        _context.Domains.Remove(domain);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ExistsAsync(long id)
    {
        return await _context.Domains.AnyAsync(e => e.Id == id);
    }

    public async Task<int> GetLinkCountByDomainIdAsync(long domainId)
    {
        var domain = await _context.Domains.FindAsync(domainId);
        if (domain == null)
        {
            return 0;
        }
        return await _context.Links.CountAsync(l => l.Domain == domain.Host);
    }
}
