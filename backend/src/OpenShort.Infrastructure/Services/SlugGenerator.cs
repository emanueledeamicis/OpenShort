using System.Security.Cryptography;
using OpenShort.Core.Interfaces;

namespace OpenShort.Infrastructure.Services;

public class SlugGenerator : ISlugGenerator
{
    private const string Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string GenerateSlug(int length = 7)
    {
        var result = new char[length];
        var data = new byte[length];
        
        using (var crypto = RandomNumberGenerator.Create())
        {
            crypto.GetBytes(data);
        }

        for (int i = 0; i < length; i++)
        {
            result[i] = Chars[data[i] % Chars.Length];
        }

        return new string(result);
    }
}
