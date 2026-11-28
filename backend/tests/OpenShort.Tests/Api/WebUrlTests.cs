using FluentAssertions;
using OpenShort.Api.Validation;

namespace OpenShort.Tests.Api;

[TestFixture]
public class WebUrlTests
{
    [TestCase("https://example.com")]
    [TestCase("http://example.com/path?x=1#top")]
    [TestCase("HTTPS://Example.com/Path")]
    [TestCase("http://localhost:8080/")]
    [TestCase("https://192.168.1.10/a")]
    [TestCase(" https://example.com/ ")]
    [TestCase("https://user:password@example.com/")]
    [TestCase("https://münchen.example/caffè")]
    public void TryParse_ShouldAccept_AnAbsoluteHttpOrHttpsUrl(string value)
    {
        WebUrl.TryParse(value, out var url).Should().BeTrue();
        url.Should().NotBeNull();
    }

    // "/path" is the case that differs between Windows and Linux, where it parses as a file URL
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-url")]
    [TestCase("example.com/path")]
    [TestCase("/path")]
    [TestCase("//example.com/path")]
    [TestCase("javascript:alert(1)")]
    [TestCase("vbscript:msgbox(1)")]
    [TestCase("data:text/html,<p>x</p>")]
    [TestCase("file:///etc/passwd")]
    [TestCase("ftp://example.com/file")]
    [TestCase("mailto:someone@example.com")]
    [TestCase("myapp://open/1")]
    [TestCase("http://")]
    [TestCase("http:example.com")]
    [TestCase("https:///path")]
    public void TryParse_ShouldRefuse_EverythingElse(string? value)
    {
        WebUrl.TryParse(value, out var url).Should().BeFalse();
        url.Should().BeNull();
    }

    [TestCase("https://example.com", "https://example.com/")]
    [TestCase("HTTPS://Example.COM:443/Path", "https://example.com/Path")]
    [TestCase("https://example.com/caffè?q=è#è", "https://example.com/caff%C3%A8?q=%C3%A8#%C3%A8")]
    [TestCase("https://example.com/caff%C3%A8", "https://example.com/caff%C3%A8")]
    [TestCase("https://example.com/a%20b c", "https://example.com/a%20b%20c")]
    [TestCase("https://example.com/?x=1%2F2&y=a+b&z=100%25", "https://example.com/?x=1%2F2&y=a+b&z=100%25")]
    [TestCase("https://münchen.example/x", "https://xn--mnchen-3ya.example/x")]
    [TestCase("https://xn--mnchen-3ya.example/x", "https://xn--mnchen-3ya.example/x")]
    [TestCase("https://example.com/日本語/😀", "https://example.com/%E6%97%A5%E6%9C%AC%E8%AA%9E/%F0%9F%98%80")]
    [TestCase("https://user:pàss@example.com/", "https://user:p%C3%A0ss@example.com/")]
    [TestCase("https://example.com/a\nb", "https://example.com/a%0Ab")]
    [TestCase("https://[::1]:8080/x", "https://[::1]:8080/x")]
    public void TryEncode_ShouldWriteTheUrl_WithAsciiCharactersOnly(string value, string expected)
    {
        WebUrl.TryParse(value, out var url).Should().BeTrue();

        WebUrl.TryEncode(url!, out var encoded).Should().BeTrue();

        encoded.Should().Be(expected);
    }

    [TestCase("https://example.com/caffè")]
    [TestCase("https://münchen.example/caffè?q=è")]
    [TestCase("https://example.com/a%20b c")]
    public void TryEncode_ShouldGiveTheSameUrl_WhenItIsAppliedTwice(string value)
    {
        WebUrl.TryParse(value, out var url).Should().BeTrue();
        WebUrl.TryEncode(url!, out var once).Should().BeTrue();
        WebUrl.TryParse(once, out var encodedUrl).Should().BeTrue();

        WebUrl.TryEncode(encodedUrl!, out var twice).Should().BeTrue();

        twice.Should().Be(once);
    }

    // A value that the header accepts is not rewritten, a space included: it worked before
    [TestCase("https://example.com/path?x=1#top")]
    [TestCase("https://example.com/a b")]
    [TestCase("https://example.com/caff%C3%A8")]
    [TestCase("ftp://example.com/file")]
    [TestCase("mailto:someone@example.com")]
    [TestCase("myapp://open/1")]
    public void TryGetLocation_ShouldLeaveTheStoredUrlAsItIs_WhenAHeaderCanCarryIt(string storedUrl)
    {
        WebUrl.TryGetLocation(storedUrl, out var location).Should().BeTrue();

        location.Should().Be(storedUrl);
    }

    // The forms stored by a version before 0.4.1, which decoded the URL
    [TestCase("https://example.com/caffè", "https://example.com/caff%C3%A8")]
    [TestCase("https://example.com/?q=caffè&r=a b", "https://example.com/?q=caff%C3%A8&r=a%20b")]
    [TestCase("https://münchen.example/x", "https://xn--mnchen-3ya.example/x")]
    [TestCase("https://example.com/a\nb", "https://example.com/a%0Ab")]
    [TestCase("ftp://münchen.example/è", "ftp://xn--mnchen-3ya.example/%C3%A8")]
    public void TryGetLocation_ShouldEncodeTheStoredUrl_WhenAHeaderCannotCarryIt(string storedUrl, string expected)
    {
        WebUrl.TryGetLocation(storedUrl, out var location).Should().BeTrue();

        location.Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("caffè")]
    public void TryGetLocation_ShouldRefuse_AValueThatIsNotAUrlAndCannotBeWritten(string? storedUrl)
    {
        WebUrl.TryGetLocation(storedUrl, out _).Should().BeFalse();
    }
}
