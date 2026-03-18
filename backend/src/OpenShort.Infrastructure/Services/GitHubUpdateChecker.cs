using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class GitHubUpdateChecker : IUpdateChecker
{
    private static readonly TimeSpan CacheInterval = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubUpdateChecker> _logger;
    private readonly GitHubUpdateOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedLatestVersion;
    private DateTime _lastCheckedAt = DateTime.MinValue;

    public GitHubUpdateChecker(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubUpdateOptions> options,
        ILogger<GitHubUpdateChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!Uri.IsWellFormedUriString(_options.ApiUrl, UriKind.Absolute))
        {
            return null;
        }

        if (DateTime.UtcNow - _lastCheckedAt < CacheInterval)
        {
            return _cachedLatestVersion;
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (DateTime.UtcNow - _lastCheckedAt < CacheInterval)
            {
                return _cachedLatestVersion;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenShort");

            var response = await client.GetFromJsonAsync<GitHubReleaseResponse>(_options.ApiUrl, cancellationToken);
            _cachedLatestVersion = response?.TagName?.TrimStart('v');
            _lastCheckedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates from GitHub");
        }
        finally
        {
            _lock.Release();
        }

        return _cachedLatestVersion;
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }
}
