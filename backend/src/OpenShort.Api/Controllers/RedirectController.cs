using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private const string RedirectProcessingErrorMessage = "An error occurred while processing the redirect.";

    private readonly ILogger<RedirectController> _logger;
    private readonly ILinkService _linkService;

    public RedirectController(ILogger<RedirectController> logger, ILinkService linkService)
    {
        _logger = logger;
        _linkService = linkService;
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

            // Get link and track click internally
            var link = await _linkService.ResolveAndTrackRedirectAsync(host, slug);

            if (link == null)
            {
                _logger.LogWarning("Redirect failed: Link not found or inactive for slug {Slug} on host {Host}", slug, host);
                return NotFound();
            }

            _logger.LogInformation("Redirecting slug {Slug} to {DestinationUrl}", slug, link.DestinationUrl);

            if (link.RedirectType == RedirectType.Permanent)
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
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: RedirectProcessingErrorMessage);
        }
    }
}
