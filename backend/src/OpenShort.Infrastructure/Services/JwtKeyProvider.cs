using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenShort.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace OpenShort.Infrastructure.Services;

public interface IJwtKeyProvider
{
    Task<string> GetOrGenerateKeyAsync();
}

// The key given in the configuration cannot sign a token: the application must not start with it
public class InvalidJwtKeyException : Exception
{
    public InvalidJwtKeyException(string message) : base(message)
    {
    }
}

public class JwtKeyProvider : IJwtKeyProvider
{
    // HS256 does not sign with a shorter key: the login would answer with an error
    public const int MinimumKeyBytes = 32;

    private static readonly string[] ConfigurationKeys = ["JWT_SECRET_KEY", "Jwt:Key"];
    private const string CacheKey = "JwtSecretKey_Cache";
    private const string JwtSecretKeySettingKey = "JwtSecretKey";
    private const string JwtSecretKeyFromConfigDescription = "JWT Secret Key (From Env/Args)";
    private const string JwtSecretKeyGeneratedDescription = "Auto-generated JWT Secret Key";
    private const string JwtKeyFoundInConfigurationMessage = "JWT Key found in configuration (Environment/Args). Using provided key.";
    private const string JwtKeyFoundInDatabaseMessage = "JWT Key found in Database Settings. Using stored key.";
    private const string JwtKeyGeneratingMessage = "No JWT Key found. Generating a new secure random key...";
    private const string JwtKeyGeneratedMessage = "New JWT Key generated and saved to Database.";
    private const string JwtKeyTooShortInConfigurationMessage = "{0} is too short: it is {1} bytes long and it must be at least {2}. The key was not saved and OpenShort does not start. Set a random value of at least {2} characters, or remove {0} to use the key stored in the database.";
    private const string JwtKeyTooShortInDatabaseMessage = "The JWT Key stored in the database is too short to sign a token ({Length} bytes, at least {Minimum} needed): no session could be opened with it. It is replaced with a new random key.";

    private readonly ISettingService _settingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtKeyProvider> _logger;
    private readonly IMemoryCache _cache;

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
        var configKeyName = ConfigurationKeys.FirstOrDefault(name => !string.IsNullOrEmpty(_configuration[name]));
        var configKey = configKeyName == null ? null : _configuration[configKeyName];

        if (!string.IsNullOrEmpty(configKey))
        {
            var configKeyBytes = Encoding.UTF8.GetByteCount(configKey);
            if (configKeyBytes < MinimumKeyBytes)
            {
                throw new InvalidJwtKeyException(string.Format(JwtKeyTooShortInConfigurationMessage, configKeyName, configKeyBytes, MinimumKeyBytes));
            }

            _logger.LogInformation(JwtKeyFoundInConfigurationMessage);
            
            // Optionally update DB to stay consistent
            await _settingService.SetSettingAsync(JwtSecretKeySettingKey, configKey, JwtSecretKeyFromConfigDescription);
            
            _cache.Set(CacheKey, configKey);
            return configKey;
        }

        // 2. Check Database Settings
        var dbKey = await _settingService.GetSettingAsync(JwtSecretKeySettingKey);
        // A short key was saved by a version that did not check JWT_SECRET_KEY: it never signed a token
        var dbKeyBytes = string.IsNullOrEmpty(dbKey) ? 0 : Encoding.UTF8.GetByteCount(dbKey);
        if (dbKeyBytes > 0 && dbKeyBytes < MinimumKeyBytes)
        {
            _logger.LogWarning(JwtKeyTooShortInDatabaseMessage, dbKeyBytes, MinimumKeyBytes);
        }
        else if (!string.IsNullOrEmpty(dbKey))
        {
            _logger.LogInformation(JwtKeyFoundInDatabaseMessage);
            
            _cache.Set(CacheKey, dbKey);
            return dbKey;
        }

        // 3. Generate a new secure key
        _logger.LogInformation(JwtKeyGeneratingMessage);
        var generatedKey = GenerateSecureKey();
        
        await _settingService.SetSettingAsync(JwtSecretKeySettingKey, generatedKey, JwtSecretKeyGeneratedDescription);
        _logger.LogInformation(JwtKeyGeneratedMessage);
        
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
