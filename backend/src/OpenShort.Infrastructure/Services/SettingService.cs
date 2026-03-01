using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class SettingService : ISettingService
{
    private readonly AppDbContext _context;

    public SettingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    public async Task<int> GetSettingIntAsync(string key, int defaultValue)
    {
        var value = await GetSettingAsync(key);
        if (int.TryParse(value, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    public async Task SetSettingAsync(string key, string value, string? description = null)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = key,
                Value = value,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            if (description != null)
            {
                setting.Description = description;
            }
            setting.UpdatedAt = DateTime.UtcNow;
            _context.SystemSettings.Update(setting);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<SystemSetting>> GetAllSettingsAsync()
    {
        return await _context.SystemSettings.ToListAsync();
    }
}
