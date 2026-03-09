using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenShort.Core.Interfaces;
using System.Security.Cryptography;

namespace OpenShort.Infrastructure.Services;

public interface IJwtKeyProvider
{
    Task<string> GetOrGenerateKeyAsync();
}

public class JwtKeyProvider : IJwtKeyProvider
{
    private readonly ISettingService _settingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtKeyProvider> _logger;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "JwtSecretKey_Cache";

    public JwtKeyProvider(ISettingService settingService, IConfiguration configuration, ILogger<JwtKeyProvider> logger, IMemoryCache cache)
    {
        _settingService = settingService;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public async Task<string> GetOrGenerateKeyAsync()
    {
        // 0. Check Memory Cache
        if (_cache.TryGetValue(CacheKey, out string? cachedKey) && !string.IsNullOrEmpty(cachedKey))
        {
            return cachedKey;
        }

        // 1. Check Configuration (Environment Variable or Command Line)
        var configKey = _configuration["JWT_SECRET_KEY"] ?? _configuration["Jwt:Key"];
        
        if (!string.IsNullOrEmpty(configKey))
        {
            _logger.LogInformation("JWT Key found in configuration (Environment/Args). Using provided key.");
            
            // Optionally update DB to stay consistent
            await _settingService.SetSettingAsync("JwtSecretKey", configKey, "JWT Secret Key (From Env/Args)");
            
            _cache.Set(CacheKey, configKey);
            return configKey;
        }

        // 2. Check Database Settings
        var dbKey = await _settingService.GetSettingAsync("JwtSecretKey");
        if (!string.IsNullOrEmpty(dbKey))
        {
            _logger.LogInformation("JWT Key found in Database Settings. Using stored key.");
            
            _cache.Set(CacheKey, dbKey);
            return dbKey;
        }

        // 3. Generate a new secure key
        _logger.LogInformation("No JWT Key found. Generating a new secure random key...");
        var generatedKey = GenerateSecureKey();
        
        await _settingService.SetSettingAsync("JwtSecretKey", generatedKey, "Auto-generated JWT Secret Key");
        _logger.LogInformation("New JWT Key generated and saved to Database.");
        
        _cache.Set(CacheKey, generatedKey);
        return generatedKey;
    }

    private static string GenerateSecureKey()
    {
        var keyBytes = new byte[64]; // 512 bits
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        return Convert.ToBase64String(keyBytes);
    }
}
