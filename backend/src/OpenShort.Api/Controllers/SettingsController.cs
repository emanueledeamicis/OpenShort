using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;

namespace OpenShort.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Only authenticated users can manage settings
public class SettingsController : ControllerBase
{
    private readonly ISettingService _settingService;

    public SettingsController(ISettingService settingService)
    {
        _settingService = settingService;
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
        if (value == null) return NotFound();
        return Ok(new { Key = key, Value = value });
    }

    [HttpPost]
    public async Task<IActionResult> SetSetting([FromBody] SetSettingDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || request.Value == null)
        {
            return BadRequest("Key and Value are required.");
        }

        await _settingService.SetSettingAsync(request.Key, request.Value, request.Description);
        return Ok();
    }
}

public class SetSettingDto
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}
