using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly AppDbContext _context;

    public RedirectController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> RedirectToUrl(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest();
        }

        var host = Request.Host.Host; // Get domain without port
        
        var link = await _context.Links
            .FirstOrDefaultAsync(l => l.Slug == slug && l.Domain == host);

        if (link == null || !link.IsActive || (link.ExpiresAt.HasValue && link.ExpiresAt < DateTime.UtcNow))
        {
            return NotFound();
        }

        // Tracking
        link.ClickCount++;
        link.LastAccessedAt = DateTime.UtcNow;
        
        // We don't want to block the redirect too long for DB writes in a real high-scale app, 
        // but for MVP direct save is fine.
        await _context.SaveChangesAsync();

        if (link.RedirectType == Core.Entities.RedirectType.Permanent)
        {
            return RedirectPermanent(link.DestinationUrl);
        }
        else
        {
            return Redirect(link.DestinationUrl);
        }
    }
}
