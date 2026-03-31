using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using System.Net;

namespace OpenShort.Api.Controllers;

[ApiController]
public class RedirectController : ControllerBase
{
    private const string RedirectProcessingErrorMessage = "An error occurred while processing the redirect.";
    private const string MissingLinkTemplateFileName = "missing-link.html";

    private readonly ILogger<RedirectController> _logger;
    private readonly ILinkService _linkService;
    private readonly IDomainService _domainService;
    private readonly IWebHostEnvironment _environment;

    public RedirectController(
        ILogger<RedirectController> logger,
        ILinkService linkService,
        IDomainService domainService,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _linkService = linkService;
        _domainService = domainService;
        _environment = environment;
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
                return await HandleMissingLinkAsync(host, slug);
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

    private async Task<IActionResult> HandleMissingLinkAsync(string host, string slug)
    {
        var domain = await _domainService.GetByHostAsync(host);

        if (domain?.NotFoundBehavior == DomainNotFoundBehavior.RedirectToCustomUrl &&
            Uri.TryCreate(domain.NotFoundRedirectUrl, UriKind.Absolute, out var customUrl))
        {
            _logger.LogInformation("Missing link on host {Host} redirected to custom 404 URL {Url}", host, customUrl);
            return Redirect(customUrl.ToString());
        }

        return BuildOpenShortNotFoundPage(host, slug);
    }

    private ContentResult BuildOpenShortNotFoundPage(string host, string slug)
    {
        var safeHost = WebUtility.HtmlEncode(host);
        var safeSlug = WebUtility.HtmlEncode(slug);
        var templatePath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), MissingLinkTemplateFileName);
        var template = System.IO.File.Exists(templatePath)
            ? System.IO.File.ReadAllText(templatePath)
            : "<!DOCTYPE html><html lang=\"en\"><body><h1>This short link is not available.</h1><p>Requested link: {{HOST}}/{{SLUG}}</p></body></html>";

        var html = template
            .Replace("{{HOST}}", safeHost, StringComparison.Ordinal)
            .Replace("{{SLUG}}", safeSlug, StringComparison.Ordinal);

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status404NotFound
        };
    }
}
