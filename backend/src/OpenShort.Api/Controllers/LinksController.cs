using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Require Authentication (JWT or ApiKey)
public class LinksController : ControllerBase
{
    private readonly ILinkService _linkService;
    private readonly IDomainService _domainService;

    public LinksController(ILinkService linkService, IDomainService domainService)
    {
        _linkService = linkService;
        _domainService = domainService;
    }

    // GET: api/Links
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Link>>> GetLinks()
    {
        return Ok(await _linkService.GetAllAsync());
    }

    // GET: api/Links/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Link>> GetLink(long id)
    {
        var link = await _linkService.GetByIdAsync(id);

        if (link == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Link not found.");
        }

        return link;
    }

    // POST: api/Links
    [HttpPost]
    public async Task<ActionResult<Link>> CreateLink(CreateLinkDto dto)
    {
        // 1. URL Validation
        if (!Uri.TryCreate(dto.DestinationUrl, UriKind.Absolute, out var uriResult))
        {
             return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Invalid Destination URL format.");
        }

        var scheme = uriResult.Scheme.ToLower();
        if (scheme == "javascript" || scheme == "vbscript" || scheme == "data")
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "URL scheme is not allowed.");
        }

        // 2. Domain Validation
        string targetDomainHost = dto.Domain;
        if (string.IsNullOrWhiteSpace(targetDomainHost)) 
        {
             return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Domain is required.");
        }

        var authorizedDomain = await _domainService.GetByHostAsync(targetDomainHost);

        if (authorizedDomain == null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"Domain '{targetDomainHost}' is not authorized.");
        }

        if (!authorizedDomain.IsActive)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"Domain '{targetDomainHost}' is not active.");
        }

        // 3. Create Link via Service
        var link = new Link
        {
            DestinationUrl = dto.DestinationUrl,
            Slug = dto.Slug, // Service handles conflict if not null, or auto-gen if null
            Domain = targetDomainHost,
            Title = dto.Title,
            Notes = dto.Notes,
            // CreatedAt/IsActive handled by Service or set here? Service sets them.
        };

        var createdLink = await _linkService.CreateAsync(link);

        if (createdLink == null)
        {
             return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Slug already in use for this domain.");
        }

        return CreatedAtAction("GetLink", new { id = createdLink.Id }, createdLink);
    }

    // PUT: api/Links/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLink(long id, Link link)
    {
        if (id != link.Id)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "ID mismatch.");
        }

        try
        {
            var success = await _linkService.UpdateAsync(link);
            if (!success)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Link not found.");
            }
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
             throw; 
        }

        return NoContent();
    }

    // DELETE: api/Links/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLink(long id)
    {
        var success = await _linkService.DeleteAsync(id);
        if (!success)
        {
             return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Link not found.");
        }

        return NoContent();
    }
}

public class CreateLinkDto
{
    public required string DestinationUrl { get; set; }
    public string? Slug { get; set; }
    public string? Domain { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
}
