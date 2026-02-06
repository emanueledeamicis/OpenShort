using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<RedirectController> _logger;

    public RedirectController(AppDbContext context, ILogger<RedirectController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> RedirectToUrl(string slug)
    {
        try
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
                _logger.LogWarning("Redirect failed: Link not found or inactive for slug {Slug} on host {Host}", slug, host);
                return NotFound();
            }

            // Tracking
            link.ClickCount++;
            link.LastAccessedAt = DateTime.UtcNow;
            
            // We don't want to block the redirect too long for DB writes in a real high-scale app, 
            // but for MVP direct save is fine.
            await _context.SaveChangesAsync();

            _logger.LogInformation("Redirecting slug {Slug} to {DestinationUrl}", slug, link.DestinationUrl);

            if (link.RedirectType == Core.Entities.RedirectType.Permanent)
            {
                return RedirectPermanent(link.DestinationUrl);
            }
            else
            {
                return Redirect(link.DestinationUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during redirection for slug {Slug}", slug);
            // We return a simple Problem detail, but usually for redirects we might just want a 500 error page.
            // Problem Details is fine for API-like behavior.
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "An error occurred while processing the redirect.");
        }
    }
}
