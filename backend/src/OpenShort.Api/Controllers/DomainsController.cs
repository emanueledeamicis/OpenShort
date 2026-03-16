using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DomainsController : ControllerBase
{
    private const string DomainNotFoundMessage = "Domain not found.";
    private const string DomainAlreadyExistsMessage = "Domain already exists.";

    private readonly IDomainService _domainService;

    public DomainsController(IDomainService domainService)
    {
        _domainService = domainService;
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
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: DomainAlreadyExistsMessage);
        }

        return CreatedAtAction("GetDomain", new { id = createdDomain.Id }, createdDomain);
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

        return NoContent();
    }
}

public class CreateDomainDto
{
    public required string Host { get; set; }
}
