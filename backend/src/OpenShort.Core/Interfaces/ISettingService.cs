using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface ISettingService
{
    Task<string?> GetSettingAsync(string key);
    Task<T> GetSettingAsync<T>(string key, T defaultValue);
    Task SetSettingAsync(string key, string value, string? description = null);
    Task<IEnumerable<SystemSetting>> GetAllSettingsAsync();
}
