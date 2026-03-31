using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DomainsController : ControllerBase
{
    private const string DomainNotFoundMessage = "Domain not found.";
    private const string DomainAlreadyExistsMessage = "Domain already exists.";
    private const string InvalidCustomUrlMessage = "A valid absolute custom 404 URL is required when redirect mode is enabled.";

    private readonly IDomainService _domainService;
    private readonly ILogger<DomainsController> _logger;

    public DomainsController(IDomainService domainService, ILogger<DomainsController> logger)
    {
        _domainService = domainService;
        _logger = logger;
    }

    // GET: api/Domains
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Domain>>> GetDomains()
    {
        return new List<Domain>(await _domainService.GetAllAsync());
    }

    // GET: api/Domains/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Domain>> GetDomain(long id)
    {
        var domain = await _domainService.GetByIdAsync(id);

        if (domain == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: DomainNotFoundMessage);
        }

        return domain;
    }

    // POST: api/Domains
    [HttpPost]
    public async Task<ActionResult<Domain>> CreateDomain(CreateDomainDto dto)
    {
        var domain = new Domain
        {
            Host = dto.Host.ToLowerInvariant(),
            IsActive = true
        };

        var createdDomain = await _domainService.CreateAsync(domain);

        if (createdDomain == null)
        {
            _logger.LogWarning("Domain creation failed because the host already exists: {Host}", dto.Host);
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: DomainAlreadyExistsMessage);
        }

        _logger.LogInformation("Domain created: {Host}", createdDomain.Host);
        return CreatedAtAction("GetDomain", new { id = createdDomain.Id }, createdDomain);
    }

    // PUT: api/Domains/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDomain(long id, UpdateDomainDto dto)
    {
        var domain = await _domainService.GetByIdAsync(id);
        if (domain == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: DomainNotFoundMessage);
        }

        if (dto.NotFoundBehavior == DomainNotFoundBehavior.RedirectToCustomUrl &&
            !Uri.TryCreate(dto.NotFoundRedirectUrl, UriKind.Absolute, out _))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: InvalidCustomUrlMessage);
        }

        domain.IsActive = dto.IsActive;
        domain.NotFoundBehavior = dto.NotFoundBehavior;
        domain.NotFoundRedirectUrl = dto.NotFoundBehavior == DomainNotFoundBehavior.RedirectToCustomUrl
            ? dto.NotFoundRedirectUrl?.Trim()
            : null;

        await _domainService.UpdateAsync(domain);

        _logger.LogInformation("Domain settings updated: {Host}", domain.Host);
        return Ok(domain);
    }

    // GET: api/Domains/5/link-count
    [HttpGet("{id}/link-count")]
    public async Task<ActionResult<int>> GetLinkCount(long id)
    {
        var domain = await _domainService.GetByIdAsync(id);
        if (domain == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: DomainNotFoundMessage);
        }

        var count = await _domainService.GetLinkCountByDomainIdAsync(id);
        return Ok(count);
    }

    // DELETE: api/Domains/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDomain(long id)
    {
        var success = await _domainService.DeleteAsync(id);
        if (!success)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: DomainNotFoundMessage);
        }

        _logger.LogInformation("Domain deleted: {DomainId}", id);
        return NoContent();
    }
}

public class CreateDomainDto
{
    public required string Host { get; set; }
}

public class UpdateDomainDto
{
    public bool IsActive { get; set; }

    public DomainNotFoundBehavior NotFoundBehavior { get; set; }

    [MaxLength(2048)]
    public string? NotFoundRedirectUrl { get; set; }
}
