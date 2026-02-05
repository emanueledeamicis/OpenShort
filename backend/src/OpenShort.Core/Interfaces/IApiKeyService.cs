using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface IApiKeyService
{
    Task<ApiKey?> GetCurrentKeyAsync();
    Task<string> GenerateNewKeyAsync(string userId, string name = "Default API Key");
    Task<bool> ValidateKeyAsync(string plainKey);
    Task UpdateLastUsedAsync(long keyId);
}
