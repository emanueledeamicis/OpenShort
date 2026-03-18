using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UpdateController : ControllerBase
{
    private readonly IUpdateChecker _updateChecker;

    public UpdateController(IUpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;
    }

    [HttpGet]
    public async Task<IActionResult> GetLatestVersion(CancellationToken cancellationToken)
    {
        var latestVersion = await _updateChecker.GetLatestVersionAsync(cancellationToken);
        return Ok(new UpdateStatusResponse { LatestVersion = latestVersion });
    }
}

public class UpdateStatusResponse
{
    public string? LatestVersion { get; set; }
}
