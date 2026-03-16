using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Only authenticated users can manage settings
public class SettingsController : ControllerBase
{
    private const string SettingNotFoundMessage = "Setting not found.";
    private const string KeyAndValueRequiredMessage = "Key and Value are required.";

    private readonly ISettingService _settingService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingService settingService, ILogger<SettingsController> logger)
    {
        _settingService = settingService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSettings()
    {
        var settings = await _settingService.GetAllSettingsAsync();
        return Ok(settings);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        var value = await _settingService.GetSettingAsync(key);
        if (value == null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: SettingNotFoundMessage);
        }

        return Ok(new { Key = key, Value = value });
    }

    [HttpPost]
    public async Task<IActionResult> SetSetting([FromBody] SetSettingDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || request.Value == null)
        {
            return BadRequest(KeyAndValueRequiredMessage);
        }

        await _settingService.SetSettingAsync(request.Key, request.Value, request.Description);
        _logger.LogInformation("Setting updated: {SettingKey}", request.Key);
        return Ok();
    }
}

public class SetSettingDto
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}
