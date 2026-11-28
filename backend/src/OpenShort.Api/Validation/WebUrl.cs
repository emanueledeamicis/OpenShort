using System.Diagnostics.CodeAnalysis;

namespace OpenShort.Api.Validation;

// The only URLs a visitor is sent to: absolute, with the http or the https scheme
public static class WebUrl
{
    public static bool TryParse(string? value, [NotNullWhen(true)] out Uri? url)
    {
        // The scheme is read from the parsed URL: on Linux "/path" parses as an absolute file URL
        if (Uri.TryCreate(value, UriKind.Absolute, out url) &&
            (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrEmpty(url.Host))
        {
            return true;
        }

        url = null;
        return false;
    }
}
