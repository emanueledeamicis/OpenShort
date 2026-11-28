using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using OpenShort.Api.Auth;
using OpenShort.Api.Controllers;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;
using OpenShort.Infrastructure.Data;
using OpenShort.Infrastructure.Services;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the security and the users controllers on an in-memory server, with Identity and the two
/// authentication schemes registered as in Program.cs: a token is accepted only while its user exists.
/// </summary>
[TestFixture]
public class SessionValidationTests
{
    private const string ApiKeyPath = "/api/security/apikey";
    private const string UsersPath = "/api/users";
    private const string Password = "StrongPass123!";
    private const string ValidApiKey = "os_valid-key-for-the-test";
    private const string SigningKey = "signing-key-used-only-by-this-test-0123456789";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    private Mock<IApiKeyService> _apiKeyServiceMock = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private AppUser _administrator = null!;
    private AppUser _otherUser = null!;

    [SetUp]
    public async Task Setup()
    {
        _apiKeyServiceMock = new Mock<IApiKeyService>();
        _apiKeyServiceMock.Setup(service => service.ValidateKeyAsync(ValidApiKey)).ReturnsAsync(true);
        _apiKeyServiceMock.Setup(service => service.GenerateNewKeyAsync(It.IsAny<string>())).ReturnsAsync("os_new-key");
        _apiKeyServiceMock
            .Setup(service => service.GetCurrentKeyAsync())
            .ReturnsAsync(new ApiKey { KeyPrefix = "os_new", CreatedAt = DateTime.UtcNow });

        var databaseName = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_apiKeyServiceMock.Object);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TwoFactorCodeThrottle>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddControllers().AddApplicationPart(typeof(SecurityController).Assembly);

        // Same schemes, token check and default policy as Program.cs
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = SessionValidation.RejectDeletedUserAsync };
            })
            .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.DefaultScheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme,
                    ApiKeyAuthOptions.DefaultScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        // Same Identity registration as Program.cs
        builder.Services.AddIdentityCore<AppUser>(AuthProtection.ConfigureIdentity)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddApiEndpoints();
        builder.Services.AddRateLimiter(AuthProtection.ConfigureRateLimiter);

        _app = builder.Build();
        _app.UseRouting();
        _app.UseRateLimiter();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        _client = _app.GetTestClient();

        using var scope = _app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        (await roleManager.CreateAsync(new IdentityRole(DatabaseInitializer.AdminRoleName))).Succeeded.Should().BeTrue();
        _administrator = await CreateAdministratorAsync(scope.ServiceProvider, "admin");
        _otherUser = await CreateAdministratorAsync(scope.ServiceProvider, "other@example.com");
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task Token_ShouldBeAccepted_WhileItsUserExists()
    {
        // Act
        using var response = await SendAsync("POST", ApiKeyPath, CreateToken(_otherUser.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_ShouldBeRejected_OnceItsUserIsDeletedFromTheUsersEndpoint()
    {
        // Arrange
        var otherUserToken = CreateToken(_otherUser.Id);
        using var before = await SendAsync("GET", UsersPath, otherUserToken);

        // Act
        using var deletion = await SendAsync("DELETE", $"{UsersPath}/{_otherUser.Id}", CreateToken(_administrator.Id));
        using var readAfter = await SendAsync("GET", UsersPath, otherUserToken);
        using var newKeyAfter = await SendAsync("POST", ApiKeyPath, otherUserToken);

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        readAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newKeyAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _apiKeyServiceMock.Verify(service => service.GenerateNewKeyAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Token_ShouldStillBeAccepted_ForTheAdministratorWhoDeletedAnotherUser()
    {
        // Arrange
        var administratorToken = CreateToken(_administrator.Id);

        // Act
        using var deletion = await SendAsync("DELETE", $"{UsersPath}/{_otherUser.Id}", administratorToken);
        using var after = await SendAsync("GET", UsersPath, administratorToken);

        // Assert
        deletion.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Token_ShouldBeRejected_WhenItsUserNeverExisted()
    {
        // Act
        using var response = await SendAsync("POST", ApiKeyPath, CreateToken("no-such-user"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // An API key is not an account: there is no user to look for
    [Test]
    public async Task ApiKey_ShouldBeAccepted_WithoutAnyUser()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiKeyPath);
        request.Headers.Add("X-Api-Key", ValidApiKey);

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // The key of an instance works whoever generated it: a request with it and the token of a deleted user
    // is the request of the key
    [Test]
    public async Task ApiKey_ShouldBeAccepted_TogetherWithTheTokenOfADeletedUser()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiKeyPath);
        request.Headers.Add("X-Api-Key", ValidApiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("no-such-user"));

        // Act
        using var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<AppUser> CreateAdministratorAsync(IServiceProvider services, string userName)
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = userName, CreatedAt = DateTime.UtcNow };
        (await userManager.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        (await userManager.AddToRoleAsync(user, DatabaseInitializer.AdminRoleName)).Succeeded.Should().BeTrue();
        return user;
    }

    private Task<HttpResponseMessage> SendAsync(string method, string path, string token)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static string CreateToken(string userId)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "user"),
                new Claim(ClaimTypes.Role, DatabaseInitializer.AdminRoleName)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = Issuer,
            Audience = Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256Signature)
        }));
    }
}
