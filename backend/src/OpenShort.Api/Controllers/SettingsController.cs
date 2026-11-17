using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Api.Controllers;

/// <summary>
/// Settings the dashboard can read and change. The settings table also holds values that belong
/// to the application only, such as the JWT signing key: this controller never returns or writes
/// a setting that is not listed in <see cref="DashboardSettings"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = DatabaseInitializer.AdminRoleName)]
public class SettingsController : ControllerBase
{
    public const int MaxCacheDurationSeconds = 86400;

    private const string SettingNotFoundMessage = "Setting not found.";
    private const string KeyAndValueRequiredMessage = "Key and Value are required.";
    private const string SettingNotEditableMessage = "This setting cannot be changed.";

    private static readonly string CacheDurationSecondsInvalidMessage =
        $"Cache duration must be a whole number of seconds between 0 and {MaxCacheDurationSeconds}.";

    // A setting missing from this list does not exist for the API, whatever the table contains.
    // Names are compared exactly: the database may match them ignoring case.
    private static readonly IReadOnlyDictionary<string, DashboardSetting> DashboardSettings =
        new Dictionary<string, DashboardSetting>(StringComparer.Ordinal)
        {
            [DatabaseInitializer.CacheDurationSecondsKey] = new DashboardSetting(
                DatabaseInitializer.CacheDurationSecondsDescription,
                CacheDurationSecondsInvalidMessage,
                NormalizeCacheDurationSeconds)
        };

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
        return Ok(settings.Where(setting => DashboardSettings.ContainsKey(setting.Key)));
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> GetSetting(string key)
    {
        if (!DashboardSettings.ContainsKey(key))
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: SettingNotFoundMessage);
        }

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
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: KeyAndValueRequiredMessage);
        }

        if (!DashboardSettings.TryGetValue(request.Key, out var setting))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: SettingNotEditableMessage);
        }

        var value = setting.Normalize(request.Value);
        if (value == null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: setting.InvalidValueMessage);
        }

        // The description sent by the client is ignored: it is not the client's to choose
        await _settingService.SetSettingAsync(request.Key, value, setting.Description);
        _logger.LogInformation("Setting updated: {SettingKey}", request.Key);
        return Ok();
    }

    private static string? NormalizeCacheDurationSeconds(string value)
    {
        return int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            && seconds <= MaxCacheDurationSeconds
            ? seconds.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <param name="Normalize">Returns the value to store, or null when the value is not valid.</param>
    private sealed record DashboardSetting(string Description, string InvalidValueMessage, Func<string, string?> Normalize);
}

public class SetSettingDto
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}
