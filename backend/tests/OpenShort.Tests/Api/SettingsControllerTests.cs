using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

[TestFixture]
public class SettingsControllerTests
{
    private const string CacheDurationSecondsKey = "CacheDurationSeconds";
    private const string JwtSecretKeyKey = "JwtSecretKey";
    private const string InitialAdminSetupRequiredKey = "InitialAdminSetupRequired";
    private const string JwtSecretKeyValue = "signing-key-that-must-never-leave-the-backend";

    private Mock<ISettingService> _settingServiceMock = null!;
    private SettingsController _controller = null!;

    [SetUp]
    public void Setup()
    {
        // The table as it is in a real installation: one dashboard setting and two internal ones
        _settingServiceMock = new Mock<ISettingService>();
        _settingServiceMock
            .Setup(service => service.GetAllSettingsAsync())
            .ReturnsAsync(new List<SystemSetting>
            {
                new() { Key = JwtSecretKeyKey, Value = JwtSecretKeyValue },
                new() { Key = CacheDurationSecondsKey, Value = "600" },
                new() { Key = InitialAdminSetupRequiredKey, Value = "false" }
            });
        _settingServiceMock.Setup(service => service.GetSettingAsync(JwtSecretKeyKey)).ReturnsAsync(JwtSecretKeyValue);
        _settingServiceMock.Setup(service => service.GetSettingAsync(CacheDurationSecondsKey)).ReturnsAsync("600");
        _settingServiceMock.Setup(service => service.GetSettingAsync(InitialAdminSetupRequiredKey)).ReturnsAsync("false");

        _controller = new SettingsController(_settingServiceMock.Object, new Mock<ILogger<SettingsController>>().Object);
    }

    [Test]
    public void SettingsController_ShouldRequireAdminJwtAuthentication()
    {
        // Act
        var attribute = typeof(SettingsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        // Assert: the API key scheme is not accepted
        attribute.AuthenticationSchemes.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        attribute.Roles.Should().Be(DatabaseInitializer.AdminRoleName);
    }

    [Test]
    public void SettingsController_ShouldNotOverrideAuthorizationOnAnyEndpoint()
    {
        // Act
        var overrides = typeof(SettingsController).GetMethods()
            .Where(method => method.DeclaringType == typeof(SettingsController))
            .SelectMany(method => method.GetCustomAttributes(inherit: true))
            .Where(attribute => attribute is AllowAnonymousAttribute or AuthorizeAttribute);

        // Assert
        overrides.Should().BeEmpty();
    }

    [Test]
    public async Task GetAllSettings_ShouldReturnOnlyDashboardSettings()
    {
        // Act
        var result = await _controller.GetAllSettings();

        // Assert
        var settings = result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IEnumerable<SystemSetting>>().Subject.ToList();
        settings.Select(setting => setting.Key).Should().Equal(CacheDurationSecondsKey);
        JsonSerializer.Serialize(settings).Should().NotContain(JwtSecretKeyValue);
    }

    [Test]
    public async Task GetSetting_ShouldReturnValue_ForADashboardSetting()
    {
        // Act
        var result = await _controller.GetSetting(CacheDurationSecondsKey);

        // Assert
        var body = JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Which.Value);
        body.Should().Contain("600");
    }

    [TestCase(JwtSecretKeyKey)]
    [TestCase(InitialAdminSetupRequiredKey)]
    [TestCase("jwtsecretkey", Description = "Some databases match setting names ignoring case")]
    [TestCase("JWTSECRETKEY")]
    [TestCase("cachedurationseconds")]
    [TestCase("Unknown")]
    public async Task GetSetting_ShouldReturnNotFoundWithoutReadingIt_ForAnyOtherSetting(string key)
    {
        // Act
        var result = await _controller.GetSetting(key);

        // Assert
        var problem = result.Should().BeOfType<ObjectResult>().Which;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        JsonSerializer.Serialize(problem.Value).Should().NotContain(JwtSecretKeyValue);
        _settingServiceMock.Verify(service => service.GetSettingAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task GetSetting_ShouldReturnNotFound_WhenADashboardSettingHasNoValueYet()
    {
        // Arrange
        _settingServiceMock.Setup(service => service.GetSettingAsync(CacheDurationSecondsKey)).ReturnsAsync((string?)null);

        // Act
        var result = await _controller.GetSetting(CacheDurationSecondsKey);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [TestCase("0", "0")]
    [TestCase("120", "120")]
    [TestCase("86400", "86400")]
    [TestCase(" 120 ", "120")]
    [TestCase("0120", "120")]
    public async Task SetSetting_ShouldStoreTheValue_ForAValidCacheDuration(string value, string storedValue)
    {
        // Act
        var result = await _controller.SetSetting(new SetSettingDto { Key = CacheDurationSecondsKey, Value = value });

        // Assert
        result.Should().BeOfType<OkResult>();
        _settingServiceMock.Verify(
            service => service.SetSettingAsync(CacheDurationSecondsKey, storedValue, It.IsAny<string>()),
            Times.Once);
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("-1")]
    [TestCase("86401")]
    [TestCase("120.5")]
    [TestCase("1e3")]
    [TestCase("+120")]
    [TestCase("99999999999999999999")]
    public async Task SetSetting_ShouldReturnBadRequestWithoutWriting_ForAnInvalidCacheDuration(string value)
    {
        // Act
        var result = await _controller.SetSetting(new SetSettingDto { Key = CacheDurationSecondsKey, Value = value });

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        VerifyNothingWasWritten();
    }

    [TestCase(JwtSecretKeyKey, "0123456789012345678901234567890123456789")]
    [TestCase(InitialAdminSetupRequiredKey, "true")]
    [TestCase("jwtsecretkey", "0123456789012345678901234567890123456789")]
    [TestCase("cachedurationseconds", "120")]
    [TestCase("Unknown", "value")]
    public async Task SetSetting_ShouldReturnBadRequestWithoutWriting_ForAnyOtherSetting(string key, string value)
    {
        // Act
        var result = await _controller.SetSetting(new SetSettingDto { Key = key, Value = value });

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        VerifyNothingWasWritten();
    }

    [TestCase("", "120")]
    [TestCase("   ", "120")]
    [TestCase(CacheDurationSecondsKey, null)]
    public async Task SetSetting_ShouldReturnBadRequestWithoutWriting_WhenKeyOrValueIsMissing(string key, string? value)
    {
        // Act
        var result = await _controller.SetSetting(new SetSettingDto { Key = key, Value = value! });

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        VerifyNothingWasWritten();
    }

    [Test]
    public async Task SetSetting_ShouldIgnoreTheDescriptionSentByTheClient()
    {
        // Act
        await _controller.SetSetting(new SetSettingDto
        {
            Key = CacheDurationSecondsKey,
            Value = "120",
            Description = "Text chosen by the client"
        });

        // Assert
        _settingServiceMock.Verify(
            service => service.SetSettingAsync(
                CacheDurationSecondsKey,
                "120",
                "Duration in seconds for which redirects are cached in memory."),
            Times.Once);
    }

    private void VerifyNothingWasWritten()
    {
        _settingServiceMock.Verify(
            service => service.SetSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }
}
