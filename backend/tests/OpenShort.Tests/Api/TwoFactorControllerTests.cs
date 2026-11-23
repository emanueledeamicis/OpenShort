using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Api.Models;
using OpenShort.Core.Entities;
using OpenShort.Infrastructure.Data;

namespace OpenShort.Tests.Api;

/// <summary>
/// Two-factor tests running against the real Identity UserManager, configured as in the application.
/// </summary>
[TestFixture]
public class TwoFactorControllerTests
{
    private const string UserName = "admin";
    private const string Password = "StrongPass123!";
    private const string WrongPassword = "WrongPass123!";

    private ServiceProvider _serviceProvider = null!;
    private UserManager<AppUser> _userManager = null!;
    private AppUser _user = null!;
    private TwoFactorController _controller = null!;

    [SetUp]
    public async Task Setup()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<AppUser>(AuthProtection.ConfigureIdentity)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        _serviceProvider = services.BuildServiceProvider();
        _userManager = _serviceProvider.GetRequiredService<UserManager<AppUser>>();

        _user = new AppUser { UserName = UserName, CreatedAt = DateTime.UtcNow };
        (await _userManager.CreateAsync(_user, Password)).Succeeded.Should().BeTrue();

        _controller = new TwoFactorController(_userManager, NullLogger<TwoFactorController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, _user.Id)],
                        JwtBearerDefaults.AuthenticationScheme))
                }
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task GetStatus_ShouldReportDisabled_ForANewUser()
    {
        // Act
        var status = await GetStatusAsync();

        // Assert
        status.IsEnabled.Should().BeFalse();
        status.RecoveryCodesLeft.Should().Be(0);
    }

    [Test]
    public async Task Setup_ShouldReturnTheSecretAndItsUri_WithoutEnablingTwoFactor()
    {
        // Act
        var setup = await SetupAsync();

        // Assert
        setup.SharedKey.Should().NotBeNullOrWhiteSpace();
        setup.AuthenticatorUri.Should().Be(
            $"otpauth://totp/OpenShort:{UserName}?secret={setup.SharedKey}&issuer=OpenShort&digits=6");
        (await GetStatusAsync()).IsEnabled.Should().BeFalse();
    }

    [Test]
    public async Task Setup_ShouldReplaceTheSecret_WhenCalledAgainBeforeTheConfirmation()
    {
        // Arrange
        var first = await SetupAsync();

        // Act
        var second = await SetupAsync();
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = TotpGenerator.Generate(first.SharedKey) });

        // Assert
        second.SharedKey.Should().NotBe(first.SharedKey);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Setup_ShouldReturnConflict_AndKeepTheSecret_WhenTwoFactorIsEnabled()
    {
        // Arrange
        var sharedKey = await EnableAsync();

        // Act
        var result = await _controller.Setup();

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
        (await _userManager.GetAuthenticatorKeyAsync(_user)).Should().Be(sharedKey);
    }

    [Test]
    public async Task Enable_ShouldEnableTwoFactorAndReturnRecoveryCodes_WhenTheCodeIsValid()
    {
        // Arrange
        var setup = await SetupAsync();

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = TotpGenerator.Generate(setup.SharedKey) });

        // Assert
        var payload = result.Result.Should().BeOfType<OkObjectResult>().Which.Value as TwoFactorRecoveryCodesResponse;
        payload!.RecoveryCodes.Should().HaveCount(TwoFactorController.RecoveryCodeCount).And.OnlyHaveUniqueItems();

        var status = await GetStatusAsync();
        status.IsEnabled.Should().BeTrue();
        status.RecoveryCodesLeft.Should().Be(TwoFactorController.RecoveryCodeCount);
    }

    [Test]
    public async Task Enable_ShouldAcceptACodeWrittenInGroups()
    {
        // Arrange
        var setup = await SetupAsync();
        var code = TotpGenerator.Generate(setup.SharedKey);

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = $" {code[..3]} {code[3..]} " });

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [TestCase("000000")]
    [TestCase("not-a-code")]
    [TestCase("")]
    public async Task Enable_ShouldReturnBadRequest_WhenTheCodeIsNotValid(string code)
    {
        // Arrange
        var setup = await SetupAsync();
        if (code == TotpGenerator.Generate(setup.SharedKey))
        {
            Assert.Ignore("The fixed code happens to be the valid one right now.");
        }

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = code });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await GetStatusAsync()).IsEnabled.Should().BeFalse();
    }

    [Test]
    public async Task Enable_ShouldReturnBadRequest_WhenTheCodeIsOld()
    {
        // Arrange
        var setup = await SetupAsync();
        var oldCode = TotpGenerator.Generate(setup.SharedKey, DateTimeOffset.UtcNow.AddMinutes(-10));

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = oldCode });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Enable_ShouldReturnBadRequest_WhenTheSetupWasNotStarted()
    {
        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = "123456" });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task Enable_ShouldReturnConflict_WhenTwoFactorIsAlreadyEnabled()
    {
        // Arrange
        var sharedKey = await EnableAsync();

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = TotpGenerator.Generate(sharedKey) });

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Test]
    public async Task RegenerateRecoveryCodes_ShouldReplaceTheCodes_WhenThePasswordIsCorrect()
    {
        // Arrange
        await EnableAsync();
        var oldCode = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 1))!.Single();

        // Act
        var result = await _controller.RegenerateRecoveryCodes(new TwoFactorPasswordRequest { Password = Password });

        // Assert
        var payload = result.Result.Should().BeOfType<OkObjectResult>().Which.Value as TwoFactorRecoveryCodesResponse;
        payload!.RecoveryCodes.Should().HaveCount(TwoFactorController.RecoveryCodeCount).And.NotContain(oldCode);
        (await _userManager.RedeemTwoFactorRecoveryCodeAsync(_user, oldCode)).Succeeded.Should().BeFalse();
        (await _userManager.RedeemTwoFactorRecoveryCodeAsync(_user, payload.RecoveryCodes[0])).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task RegenerateRecoveryCodes_ShouldReturnBadRequest_AndKeepTheCodes_WhenThePasswordIsWrong()
    {
        // Arrange
        await EnableAsync();
        var currentCode = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 1))!.Single();

        // Act
        var result = await _controller.RegenerateRecoveryCodes(new TwoFactorPasswordRequest { Password = WrongPassword });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await _userManager.RedeemTwoFactorRecoveryCodeAsync(_user, currentCode)).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task RegenerateRecoveryCodes_ShouldReturnConflict_WhenTwoFactorIsNotEnabled()
    {
        // Act
        var result = await _controller.RegenerateRecoveryCodes(new TwoFactorPasswordRequest { Password = Password });

        // Assert
        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Test]
    public async Task Disable_ShouldTurnTwoFactorOffAndForgetTheSecret_WhenThePasswordIsCorrect()
    {
        // Arrange
        await EnableAsync();

        // Act
        var result = await _controller.Disable(new TwoFactorPasswordRequest { Password = Password });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (await GetStatusAsync()).IsEnabled.Should().BeFalse();
        (await _userManager.GetAuthenticatorKeyAsync(_user)).Should().BeNull();
        (await _userManager.CountRecoveryCodesAsync(_user)).Should().Be(0);
    }

    [Test]
    public async Task Disable_ShouldReturnBadRequest_AndKeepTwoFactorOn_WhenThePasswordIsWrong()
    {
        // Arrange
        var sharedKey = await EnableAsync();

        // Act
        var result = await _controller.Disable(new TwoFactorPasswordRequest { Password = WrongPassword });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        (await GetStatusAsync()).IsEnabled.Should().BeTrue();
        (await _userManager.GetAuthenticatorKeyAsync(_user)).Should().Be(sharedKey);
    }

    [Test]
    public async Task Disable_ShouldReturnConflict_WhenTwoFactorIsNotEnabled()
    {
        // Act
        var result = await _controller.Disable(new TwoFactorPasswordRequest { Password = Password });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Test]
    public async Task Enable_ShouldNeedANewSetup_AfterTwoFactorWasDisabled()
    {
        // Arrange
        var sharedKey = await EnableAsync();
        await _controller.Disable(new TwoFactorPasswordRequest { Password = Password });

        // Act
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = TotpGenerator.Generate(sharedKey) });

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await GetStatusAsync()).IsEnabled.Should().BeFalse();
    }

    [Test]
    public async Task Endpoints_ShouldReturnUnauthorized_WhenTheUserNoLongerExists()
    {
        // Arrange
        (await _userManager.DeleteAsync(_user)).Succeeded.Should().BeTrue();

        // Act and assert
        (await _controller.GetStatus()).Result.Should().BeOfType<UnauthorizedResult>();
        (await _controller.Setup()).Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Test]
    public void Controller_ShouldAcceptTheDashboardTokenOnly()
    {
        // Act
        var attribute = typeof(TwoFactorController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        // Assert: an API key must not be able to manage the second factor of an account
        attribute.AuthenticationSchemes.Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }

    [TestCase(nameof(TwoFactorController.Setup))]
    [TestCase(nameof(TwoFactorController.Enable))]
    [TestCase(nameof(TwoFactorController.RegenerateRecoveryCodes))]
    [TestCase(nameof(TwoFactorController.Disable))]
    public void Endpoint_ShouldBeRateLimited(string methodName)
    {
        // Act
        var attribute = typeof(TwoFactorController).GetMethod(methodName)!
            .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>()
            .Single();

        // Assert
        attribute.PolicyName.Should().Be(AuthProtection.RateLimitPolicy);
    }

    private async Task<TwoFactorStatusResponse> GetStatusAsync()
    {
        var result = await _controller.GetStatus();
        return (TwoFactorStatusResponse)result.Result.Should().BeOfType<OkObjectResult>().Which.Value!;
    }

    private async Task<TwoFactorSetupResponse> SetupAsync()
    {
        var result = await _controller.Setup();
        return (TwoFactorSetupResponse)result.Result.Should().BeOfType<OkObjectResult>().Which.Value!;
    }

    private async Task<string> EnableAsync()
    {
        var setup = await SetupAsync();
        var result = await _controller.Enable(new EnableTwoFactorRequest { Code = TotpGenerator.Generate(setup.SharedKey) });
        result.Result.Should().BeOfType<OkObjectResult>();
        return setup.SharedKey;
    }
}
