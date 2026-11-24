using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;
using System.Threading.Channels;

namespace OpenShort.Tests.Infrastructure;

// The public redirect, with the cache turned on: what a visitor gets after a link or a domain changes
[TestFixture]
public class RedirectResolutionTests
{
    private const string Host = "test.com";
    private const string Slug = "promo";

    private AppDbContext _context = null!;
    private MemoryCache _memoryCache = null!;
    private RedirectCache _redirectCache = null!;
    private Channel<ClickEvent> _clickChannel = null!;
    private LinkService _linkService = null!;
    private DomainService _domainService = null!;
    private Domain _domain = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _redirectCache = new RedirectCache(_memoryCache);
        _clickChannel = Channel.CreateUnbounded<ClickEvent>();

        var settingService = new Mock<ISettingService>();
        settingService.Setup(s => s.GetSettingAsync("CacheDurationSeconds", It.IsAny<int>())).ReturnsAsync(600);

        _linkService = new LinkService(
            _context,
            new Mock<ISlugGenerator>().Object,
            _redirectCache,
            settingService.Object,
            _clickChannel.Writer,
            new Mock<ILogger<LinkService>>().Object);
        _domainService = new DomainService(_context, _redirectCache);

        _domain = new Domain { Host = Host, IsActive = true };
        _context.Domains.Add(_domain);
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _memoryCache.Dispose();
        _context.Dispose();
    }

    [Test]
    public async Task Resolve_ShouldReturnTheLinkAndTrackTheClick()
    {
        await AddLinkAsync();

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().NotBeNull();
        link!.DestinationUrl.Should().Be("https://example.com");
        _clickChannel.Reader.Count.Should().Be(1);
    }

    [Test]
    public async Task Resolve_ShouldAnswerFromTheCache_WhenTheLinkWasAlreadyResolved()
    {
        var stored = await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        // Removed behind the back of the service, which would empty the cache
        _context.Links.Remove(stored);
        await _context.SaveChangesAsync();

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().NotBeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnNull_WhenTheLinkIsInactive()
    {
        await AddLinkAsync(isActive: false);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
        _clickChannel.Reader.Count.Should().Be(0);
    }

    [Test]
    public async Task Resolve_ShouldReturnNull_WhenTheLinkIsExpired()
    {
        await AddLinkAsync(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnNull_WhenTheLinkExpiresWhileInTheCache()
    {
        var stored = await AddLinkAsync(expiresAt: DateTime.UtcNow.AddMinutes(5));
        (await _linkService.ResolveAndTrackRedirectAsync(Host, Slug)).Should().NotBeNull();

        // The cache holds this same object
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
        _clickChannel.Reader.Count.Should().Be(1);
    }

    [Test]
    public async Task Resolve_ShouldReturnNull_WhenTheDomainIsInactive()
    {
        _domain.IsActive = false;
        await _context.SaveChangesAsync();
        await AddLinkAsync();

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
        _clickChannel.Reader.Count.Should().Be(0);
    }

    [Test]
    public async Task Resolve_ShouldReturnTheLink_WhenItsDomainIsNotRegistered()
    {
        await AddLinkAsync(host: "unregistered.com");

        var link = await _linkService.ResolveAndTrackRedirectAsync("unregistered.com", Slug);

        link.Should().NotBeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnNullAtOnce_WhenTheDomainIsTurnedOff()
    {
        await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        _domain.IsActive = false;
        await _domainService.UpdateAsync(_domain);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnTheLinkAgain_WhenTheDomainIsTurnedBackOn()
    {
        await AddLinkAsync();
        _domain.IsActive = false;
        await _domainService.UpdateAsync(_domain);
        (await _linkService.ResolveAndTrackRedirectAsync(Host, Slug)).Should().BeNull();

        _domain.IsActive = true;
        await _domainService.UpdateAsync(_domain);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().NotBeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnNullAtOnce_WhenTheDomainIsDeleted()
    {
        await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        await _domainService.DeleteAsync(_domain.Id);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
    }

    [Test]
    public async Task UpdateDomain_ShouldKeepTheCache_WhenTheDomainStaysActive()
    {
        await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        _domain.NotFoundBehavior = DomainNotFoundBehavior.RedirectToCustomUrl;
        _domain.NotFoundRedirectUrl = "https://example.com/not-found";
        await _domainService.UpdateAsync(_domain);

        _redirectCache.TryGet(Host, Slug, out _).Should().BeTrue();
    }

    [Test]
    public async Task Set_ShouldNotStoreTheLink_WhenTheCacheWasEmptiedAfterItWasLoaded()
    {
        var stored = await AddLinkAsync();
        var generation = _redirectCache.CurrentGeneration;

        _redirectCache.Clear();
        _redirectCache.Set(Host, Slug, stored, TimeSpan.FromMinutes(10), generation);

        _redirectCache.TryGet(Host, Slug, out _).Should().BeFalse();
    }

    [Test]
    public async Task Set_ShouldStoreTheLink_AfterTheCacheWasEmptied()
    {
        var stored = await AddLinkAsync();
        _redirectCache.Clear();

        _redirectCache.Set(Host, Slug, stored, TimeSpan.FromMinutes(10), _redirectCache.CurrentGeneration);

        _redirectCache.TryGet(Host, Slug, out var link).Should().BeTrue();
        link.Should().BeSameAs(stored);
    }

    private async Task<Link> AddLinkAsync(string host = Host, bool isActive = true, DateTime? expiresAt = null)
    {
        var link = new Link
        {
            Slug = Slug,
            Domain = host,
            DestinationUrl = "https://example.com",
            IsActive = isActive,
            ExpiresAt = expiresAt
        };

        _context.Links.Add(link);
        await _context.SaveChangesAsync();
        return link;
    }
}
