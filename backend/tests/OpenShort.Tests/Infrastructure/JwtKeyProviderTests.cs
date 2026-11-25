using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Infrastructure;

[TestFixture]
public class JwtKeyProviderTests
{
    private const string SettingKey = "JwtSecretKey";
    private static readonly string ValidKey = new('k', JwtKeyProvider.MinimumKeyBytes);
    private static readonly string ShortKey = new('k', JwtKeyProvider.MinimumKeyBytes - 1);

    private Mock<ISettingService> _settingService = null!;
    private MemoryCache _cache = null!;
    private string? _storedKey;

    [SetUp]
    public void Setup()
    {
        _storedKey = null;
        _cache = new MemoryCache(new MemoryCacheOptions());

        _settingService = new Mock<ISettingService>();
        _settingService.Setup(s => s.GetSettingAsync(SettingKey)).ReturnsAsync(() => _storedKey);
        _settingService
            .Setup(s => s.SetSettingAsync(SettingKey, It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string, string?>((_, value, _) => _storedKey = value)
            .Returns(Task.CompletedTask);
    }

    [TearDown]
    public void TearDown()
    {
        _cache.Dispose();
    }

    [Test]
    public async Task GetOrGenerateKey_ShouldUseAndStoreTheConfiguredKey_WhenItIsLongEnough()
    {
        _storedKey = new string('o', 64);

        var key = await CreateProvider(("JWT_SECRET_KEY", ValidKey)).GetOrGenerateKeyAsync();

        key.Should().Be(ValidKey);
        _storedKey.Should().Be(ValidKey);
    }

    [TestCase("JWT_SECRET_KEY")]
    [TestCase("Jwt:Key")]
    public async Task GetOrGenerateKey_ShouldThrowAndKeepTheStoredKey_WhenTheConfiguredKeyIsTooShort(string name)
    {
        var storedKey = new string('o', 64);
        _storedKey = storedKey;

        var act = () => CreateProvider((name, ShortKey)).GetOrGenerateKeyAsync();

        (await act.Should().ThrowAsync<InvalidJwtKeyException>())
            .WithMessage($"{name} is too short*{JwtKeyProvider.MinimumKeyBytes - 1} bytes*at least {JwtKeyProvider.MinimumKeyBytes}*");
        _storedKey.Should().Be(storedKey);
        _settingService.Verify(s => s.SetSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task GetOrGenerateKey_ShouldNotShowTheKey_WhenTheConfiguredKeyIsTooShort()
    {
        const string secret = "a-short-secret";

        var act = () => CreateProvider(("JWT_SECRET_KEY", secret)).GetOrGenerateKeyAsync();

        (await act.Should().ThrowAsync<InvalidJwtKeyException>()).Which.Message.Should().NotContain(secret);
    }

    // The limit is in bytes, as the signature counts them
    [Test]
    public async Task GetOrGenerateKey_ShouldCountBytes_WhenTheConfiguredKeyHasNonAsciiCharacters()
    {
        var key = new string('é', JwtKeyProvider.MinimumKeyBytes / 2);

        var result = await CreateProvider(("JWT_SECRET_KEY", key)).GetOrGenerateKeyAsync();

        result.Should().Be(key);
    }

    [Test]
    public async Task GetOrGenerateKey_ShouldUseTheStoredKey_WhenNothingIsConfigured()
    {
        _storedKey = ValidKey;

        var key = await CreateProvider().GetOrGenerateKeyAsync();

        key.Should().Be(ValidKey);
        _settingService.Verify(s => s.SetSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task GetOrGenerateKey_ShouldGenerateAndStoreAKey_WhenThereIsNone()
    {
        var key = await CreateProvider().GetOrGenerateKeyAsync();

        System.Text.Encoding.UTF8.GetByteCount(key).Should().BeGreaterThanOrEqualTo(JwtKeyProvider.MinimumKeyBytes);
        _storedKey.Should().Be(key);
    }

    // What a version that did not check JWT_SECRET_KEY left behind
    [Test]
    public async Task GetOrGenerateKey_ShouldReplaceTheStoredKey_WhenItIsTooShort()
    {
        _storedKey = ShortKey;

        var key = await CreateProvider().GetOrGenerateKeyAsync();

        key.Should().NotBe(ShortKey);
        System.Text.Encoding.UTF8.GetByteCount(key).Should().BeGreaterThanOrEqualTo(JwtKeyProvider.MinimumKeyBytes);
        _storedKey.Should().Be(key);
    }

    private JwtKeyProvider CreateProvider(params (string Name, string Value)[] configuration)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuration.ToDictionary(item => item.Name, item => (string?)item.Value))
            .Build();

        return new JwtKeyProvider(_settingService.Object, config, new Mock<ILogger<JwtKeyProvider>>().Object, _cache);
    }
}
