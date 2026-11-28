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
    private Mock<ISettingService> _settingService = null!;
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

        _settingService = new Mock<ISettingService>();
        SetCacheDuration(600);

        _linkService = new LinkService(
            _context,
            new Mock<ISlugGenerator>().Object,
            _redirectCache,
            _settingService.Object,
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

    [TestCase("TEST.com", "promo")]
    [TestCase("test.com", "Promo")]
    [TestCase("Test.Com", "PROMO")]
    public async Task Resolve_ShouldReturnTheLink_WhateverTheCaseOfTheHostAndOfTheSlug(string host, string slug)
    {
        await AddLinkAsync();

        var link = await _linkService.ResolveAndTrackRedirectAsync(host, slug);

        link.Should().NotBeNull();
        link!.Slug.Should().Be(Slug);
    }

    // The click is counted by looking the link up again: it must carry the host and the slug as they are stored
    [Test]
    public async Task Resolve_ShouldTrackTheClickOnTheStoredLink_WhenTheRequestIsInAnotherCase()
    {
        await AddLinkAsync();

        await _linkService.ResolveAndTrackRedirectAsync("TEST.com", "Promo");

        _clickChannel.Reader.TryRead(out var click).Should().BeTrue();
        click!.Domain.Should().Be(Host);
        click.Slug.Should().Be(Slug);
    }

    // One cache entry for a link: a change must not leave the link alive under another spelling of its slug
    [Test]
    public async Task Resolve_ShouldReturnNullAtOnce_WhenTheLinkIsTurnedOffAfterARequestInAnotherCase()
    {
        var stored = await AddLinkAsync();
        (await _linkService.ResolveAndTrackRedirectAsync(Host, "Promo")).Should().NotBeNull();

        stored.IsActive = false;
        await _linkService.UpdateAsync(stored);

        (await _linkService.ResolveAndTrackRedirectAsync(Host, "Promo")).Should().BeNull();
        (await _linkService.ResolveAndTrackRedirectAsync(Host, Slug)).Should().BeNull();
    }

    [Test]
    public async Task Resolve_ShouldReturnNull_WhenTheDomainIsInactive_WhateverTheCaseOfTheHost()
    {
        _domain.IsActive = false;
        await _context.SaveChangesAsync();
        await AddLinkAsync();

        (await _linkService.ResolveAndTrackRedirectAsync("TEST.com", Slug)).Should().BeNull();
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
    public async Task Resolve_ShouldNotStoreTheLink_WhenTheCacheIsTurnedOff()
    {
        SetCacheDuration(0);
        await AddLinkAsync();

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().NotBeNull();
        _redirectCache.TryGet(Host, Slug, out _).Should().BeFalse();
    }

    [Test]
    public async Task Resolve_ShouldReturnNullAtOnce_WhenTheLinkIsTurnedOff()
    {
        var stored = await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        stored.IsActive = false;
        await _linkService.UpdateAsync(stored);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
        _redirectCache.TryGet(Host, Slug, out _).Should().BeFalse();
    }

    [Test]
    public async Task Resolve_ShouldReturnNullAtOnce_WhenTheLinkIsDeleted()
    {
        var stored = await AddLinkAsync();
        await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        await _linkService.DeleteAsync(stored.Id);

        var link = await _linkService.ResolveAndTrackRedirectAsync(Host, Slug);

        link.Should().BeNull();
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
    public async Task Resolve_ShouldReturnNull_WhenItsDomainIsNotRegistered()
    {
        await AddLinkAsync(host: "unregistered.com");

        var link = await _linkService.ResolveAndTrackRedirectAsync("unregistered.com", Slug);

        link.Should().BeNull();
        _clickChannel.Reader.Count.Should().Be(0);
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
        var snapshot = _redirectCache.TakeSnapshot();

        _redirectCache.Clear();
        _redirectCache.Set(Host, Slug, stored, TimeSpan.FromMinutes(10), snapshot);

        _redirectCache.TryGet(Host, Slug, out _).Should().BeFalse();
    }

    [Test]
    public async Task Set_ShouldStoreTheLink_AfterTheCacheWasEmptied()
    {
        var stored = await AddLinkAsync();
        _redirectCache.Clear();

        _redirectCache.Set(Host, Slug, stored, TimeSpan.FromMinutes(10), _redirectCache.TakeSnapshot());

        _redirectCache.TryGet(Host, Slug, out var link).Should().BeTrue();
        link.Should().BeSameAs(stored);
    }

    // The request that loaded a link while it was being changed must not put the old one back
    [Test]
    public async Task Set_ShouldNotStoreTheLink_WhenALinkWasRemovedAfterItWasLoaded()
    {
        var stored = await AddLinkAsync();
        var snapshot = _redirectCache.TakeSnapshot();

        _redirectCache.Remove(Host, Slug);
        _redirectCache.Set(Host, Slug, stored, TimeSpan.FromMinutes(10), snapshot);

        _redirectCache.TryGet(Host, Slug, out _).Should().BeFalse();
    }

    [Test]
    public async Task Remove_ShouldFindTheLink_WhateverTheCaseOfTheHost()
    {
        var stored = await AddLinkAsync();
        _redirectCache.Set("TEST.com", Slug, stored, TimeSpan.FromMinutes(10), _redirectCache.TakeSnapshot());

        _redirectCache.Remove(Host, Slug);

        _redirectCache.TryGet("TEST.com", Slug, out _).Should().BeFalse();
    }

    private void SetCacheDuration(int seconds)
    {
        _settingService.Setup(s => s.GetSettingAsync("CacheDurationSeconds", It.IsAny<int>())).ReturnsAsync(seconds);
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
