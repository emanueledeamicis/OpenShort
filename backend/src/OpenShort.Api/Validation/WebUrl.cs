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

    // The form that is stored and sent to the visitor: the same URL written with ASCII characters
    // only, as a Location header requires. A letter with an accent becomes its percent-encoding, an
    // international host its "xn--" form. What is already encoded is left as it is
    public static bool TryEncode(Uri url, out string encoded)
    {
        try
        {
            encoded = new UriBuilder(url) { Host = url.IdnHost }.Uri.AbsoluteUri;
        }
        catch (UriFormatException)
        {
            encoded = string.Empty;
            return false;
        }

        return CanBeWrittenInAHeader(encoded);
    }

    // The value of the Location header for a stored URL. A value that can be written as it is stays
    // untouched, whatever its scheme: the links that work keep answering the same way. The others
    // were stored decoded by a version before 0.4.1, and are encoded here
    public static bool TryGetLocation(string? storedUrl, out string location)
    {
        location = storedUrl ?? string.Empty;
        if (location.Length > 0 && CanBeWrittenInAHeader(location))
        {
            return true;
        }

        return Uri.TryCreate(storedUrl, UriKind.Absolute, out var url) && TryEncode(url, out location);
    }

    private static bool CanBeWrittenInAHeader(string value)
    {
        foreach (var character in value)
        {
            if (character < ' ' || character > '~')
            {
                return false;
            }
        }

        return true;
    }
}
