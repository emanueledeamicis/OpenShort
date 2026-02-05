using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Infrastructure.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly AppDbContext _context;
    private const string KeyPrefix = "os_";
    private const int KeyLength = 32;

    public ApiKeyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetCurrentKeyAsync()
    {
        return await _context.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<string> GenerateNewKeyAsync(string userId, string name = "Default API Key")
    {
        // Delete existing keys (only one allowed)
        var existingKeys = await _context.ApiKeys.ToListAsync();
        _context.ApiKeys.RemoveRange(existingKeys);

        // Generate new random key
        var randomBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, KeyLength);
        
        var plainKey = $"{KeyPrefix}{randomPart}";
        var keyHash = HashKey(plainKey);
        var visiblePrefix = plainKey.Substring(0, 11) + "..."; // "os_xxxxxxxx..."

        var apiKey = new ApiKey
        {
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = visiblePrefix,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        return plainKey; // Return plain key only this once
    }

    public async Task<bool> ValidateKeyAsync(string plainKey)
    {
        if (string.IsNullOrEmpty(plainKey) || !plainKey.StartsWith(KeyPrefix))
        {
            return false;
        }

        var keyHash = HashKey(plainKey);
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        return apiKey != null;
    }

    public async Task UpdateLastUsedAsync(long keyId)
    {
        var apiKey = await _context.ApiKeys.FindAsync(keyId);
        if (apiKey != null)
        {
            apiKey.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private static string HashKey(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(bytes);
    }
}
