using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenShort.Api.Controllers;
using OpenShort.Api.Validation;
using OpenShort.Core.Entities;
using OpenShort.Core.Interfaces;

namespace OpenShort.Tests.Api;

/// <summary>
/// Runs the links controller on an in-memory server: the limits declared on the request bodies and the
/// answer to a body that does not keep them are applied by the framework, before the controller is called.
/// </summary>
[TestFixture]
public class ValidationOverHttpTests
{
    private Mock<ILinkService> _linkServiceMock = null!;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task Setup()
    {
        _linkServiceMock = new Mock<ILinkService>();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_linkServiceMock.Object);
        builder.Services.AddSingleton(new Mock<IDomainService>().Object);

        // Same option as Program.cs
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(LinksController).Assembly)
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = ValidationProblemResponse.Build);
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthorization();
        // The login is not what is tested here
        _app.MapControllers().AllowAnonymous();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequestWithTheReasonInTheDetail_WhenAFieldIsTooLong()
    {
        // Act
        using var response = await _client.PostAsJsonAsync("/api/links", new
        {
            destinationUrl = "https://example.com/",
            domain = "localhost",
            title = new string('t', 101)
        });
        var problem = await ReadProblemAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        problem.GetProperty("detail").GetString().Should().Be("The field Title must be a string with a maximum length of 100.");
        problem.GetProperty("errors").GetProperty("Title")[0].GetString().Should().Be("The field Title must be a string with a maximum length of 100.");
        _linkServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task CreateLink_ShouldReturnBadRequestWithTheReasonInTheDetail_WhenTheDestinationIsMissing()
    {
        // Act
        using var response = await _client.PostAsJsonAsync("/api/links", new { domain = "localhost" });
        var problem = await ReadProblemAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("detail").GetString().Should().Contain("destinationUrl");
        _linkServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UpdateLink_ShouldReturnNoContent_WhenATitleOverTheLimitIsNotChanged()
    {
        // Arrange: a title stored by an earlier version, which did not check the length
        var title = new string('t', 101);
        _linkServiceMock
            .Setup(service => service.GetByIdAsync(7))
            .ReturnsAsync(new Link { Id = 7, Slug = "stored", Domain = "localhost", DestinationUrl = "https://example.com/", Title = title });
        _linkServiceMock.Setup(service => service.UpdateAsync(It.IsAny<Link>())).ReturnsAsync(true);

        // Act
        using var response = await _client.PutAsJsonAsync("/api/links/7", new
        {
            destinationUrl = "https://example.com/",
            title,
            isActive = false
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _linkServiceMock.Verify(service => service.UpdateAsync(It.Is<Link>(link => !link.IsActive && link.Title == title)), Times.Once);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    }
}
