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

    // PUT: api/Domains/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDomain(long id, Domain domain)
    {
        if (id != domain.Id)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "ID mismatch.");
        }

        // Service.UpdateAsync handles concurrency and returns false if not found? 
        // We defined it to return bool.
        // It throws DbUpdateConcurrencyException if concurrency, we can let it bubble or handle global?
        // Contoller had explicit catch. Service replicates it?
        // My Service implementation re-throws if exists but concurrent.
        // If not exists, returns false.
        
        try
        {
            var success = await _domainService.UpdateAsync(domain);
            if (!success)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Domain not found.");
            }
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // If service re-threw, it means it exists but conflict? 
            // Actually service only throws if ExistsAsync returns true.
            // So default behavior.
            throw;
        }

        return NoContent();
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
