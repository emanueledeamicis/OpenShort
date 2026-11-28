using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace OpenShort.Api.Validation;

// The answer to a request body that fails validation
public static class ValidationProblemResponse
{
    // The standard validation problem, with the messages also in "detail", where the other errors
    // of the API say what is wrong: a program that reads only "detail" gets the reason
    public static IActionResult Build(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
        problem.Detail = string.Join(" ", problem.Errors.SelectMany(error => error.Value).Distinct());

        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
