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
    public void TryParse_ShouldRefuse_EverythingElse(string? value)
    {
        WebUrl.TryParse(value, out var url).Should().BeFalse();
        url.Should().BeNull();
    }
}
