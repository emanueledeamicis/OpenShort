using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenShort.Api.Controllers;
using OpenShort.Api.Validation;
using System.ComponentModel.DataAnnotations;

namespace OpenShort.Tests.Api;

// The limits declared on the request bodies, and the answer the API gives when one is not kept
[TestFixture]
public class RequestValidationTests
{
    [Test]
    public void CreateLinkDto_ShouldBeValid_AtTheMaximumLengths()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://example.com/" + new string('a', 2028),
            Slug = new string('a', 50),
            Title = new string('a', 100),
            Notes = new string('a', 500)
        };

        Validate(dto).Should().BeEmpty();
    }

    [Test]
    public void CreateLinkDto_ShouldNameEveryField_OverItsMaximumLength()
    {
        var dto = new CreateLinkDto
        {
            DestinationUrl = "https://example.com/" + new string('a', 2029),
            Slug = new string('a', 51),
            Title = new string('a', 101),
            Notes = new string('a', 501)
        };

        Validate(dto).Should().BeEquivalentTo("DestinationUrl", "Slug", "Title", "Notes");
    }

    [Test]
    public void UpdateLinkDto_ShouldNameEveryField_OverItsMaximumLength()
    {
        var dto = new UpdateLinkDto
        {
            DestinationUrl = "https://example.com/" + new string('a', 2029),
            Title = new string('a', 101),
            Notes = new string('a', 501)
        };

        Validate(dto).Should().BeEquivalentTo("DestinationUrl", "Title", "Notes");
    }

    [Test]
    public void CreateDomainDto_ShouldRefuseAHost_OverItsMaximumLength()
    {
        Validate(new CreateDomainDto { Host = new string('a', 255) }).Should().BeEmpty();
        Validate(new CreateDomainDto { Host = new string('a', 256) }).Should().BeEquivalentTo("Host");
    }

    [Test]
    public void Build_ShouldPutTheMessagesInTheDetail()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Title", "The field Title must be a string with a maximum length of 100.");
        modelState.AddModelError("Notes", "The field Notes must be a string with a maximum length of 500.");
        var context = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);

        var result = OpenShort.Api.Validation.ValidationProblem.Build(context);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Keys.Should().BeEquivalentTo("Title", "Notes");
        problem.Detail.Should().Be(
            "The field Notes must be a string with a maximum length of 500. " +
            "The field Title must be a string with a maximum length of 100.");
    }

    private static IEnumerable<string> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results.SelectMany(result => result.MemberNames);
    }
}
