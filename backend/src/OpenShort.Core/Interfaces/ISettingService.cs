using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface ISettingService
{
    Task<string?> GetSettingAsync(string key);
    Task<int> GetSettingIntAsync(string key, int defaultValue);
    Task SetSettingAsync(string key, string value, string? description = null);
    Task<IEnumerable<SystemSetting>> GetAllSettingsAsync();
}
