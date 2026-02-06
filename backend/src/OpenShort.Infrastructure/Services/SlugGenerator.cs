using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OpenShort.Core;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

/// <summary>
/// Generates URL-safe slugs using cryptographically secure random bytes.
/// </summary>
public class SlugGenerator : ISlugGenerator
{
    private readonly SlugSettings _settings;
    
    // URL-safe alphabet (lowercase alphanumeric only - avoids case sensitivity issues)
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    public SlugGenerator(IOptions<SlugSettings> options)
    {
        _settings = options.Value;
    }

    public string GenerateSlug()
    {
        var length = _settings.Length;
        var result = new char[length];
        var randomBytes = RandomNumberGenerator.GetBytes(length);
        
        for (int i = 0; i < length; i++)
        {
            result[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }
        
        return new string(result);
    }
}
