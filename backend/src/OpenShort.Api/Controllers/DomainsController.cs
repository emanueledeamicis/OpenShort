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
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Domain not found.");
        }

        return domain;
    }

    // POST: api/Domains
    [HttpPost]
    public async Task<ActionResult<Domain>> CreateDomain(CreateDomainDto dto)
    {
        // Check manually if desired, but Service Create returns null on conflict
        // Or we can check ExistsByHost logic? 
        // Service CreateAsync returns null if exists.
        
        var domain = new Domain
        {
            Host = dto.Host,
            IsActive = true
        };

        var createdDomain = await _domainService.CreateAsync(domain);

        if (createdDomain == null)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Domain already exists.");
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
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Domain not found.");
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
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Domain not found.");
        }

        return NoContent();
    }
}

public class CreateDomainDto
{
    public required string Host { get; set; }
}
