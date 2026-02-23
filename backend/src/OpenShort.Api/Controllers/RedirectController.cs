using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using System.Threading.Channels;

namespace OpenShort.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private readonly ILogger<RedirectController> _logger;
    private readonly ILinkService _linkService;
    private readonly ChannelWriter<ClickEvent> _clickChannel;

    public RedirectController(ILogger<RedirectController> logger, ILinkService linkService, ChannelWriter<ClickEvent> clickChannel)
    {
        _logger = logger;
        _linkService = linkService;
        _clickChannel = clickChannel;
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

            // Get link (either from cache or DB)
            var link = await _linkService.GetCachedLinkAsync(host, slug);

            if (link == null)
            {
                _logger.LogWarning("Redirect failed: Link not found or inactive for slug {Slug} on host {Host}", slug, host);
                return NotFound();
            }

            // Tracking asynchronously - Fire and Forget via Channel
            var clickEvent = new ClickEvent
            {
                Slug = slug,
                Domain = host,
                Timestamp = DateTime.UtcNow
            };
            
            // TryWrite is fire-and-forget, non-blocking
            if (!_clickChannel.TryWrite(clickEvent))
            {
                _logger.LogWarning("Failed to enqueue click event for {Domain}/{Slug}. Channel might be full.", host, slug);
            }

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
