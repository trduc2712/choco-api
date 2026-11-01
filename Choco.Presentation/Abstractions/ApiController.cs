using Choco.Presentation.Extensions;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Choco.Presentation.Abstractions;

[ApiController]
[Authorize]
public abstract class ApiController : ControllerBase
{
    protected ISender Sender { get; }

    protected ApiController(ISender sender)
    {
        Sender = sender;
    }

    protected IActionResult HandleFailure(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot handle a successful result as a failure.");
        }

        int statusCode = result.Error.Type.ToStatusCode();

        ProblemDetails problemDetails = result.Error is ValidationError validationError
            ? CreateValidationProblemDetails(validationError, statusCode)
            : ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: statusCode, detail: result.Error.Description);

        problemDetails.Extensions["code"] = result.Error.Code;

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private ValidationProblemDetails CreateValidationProblemDetails(ValidationError validationError, int statusCode)
    {
        ModelStateDictionary modelState = new();

        foreach ((string propertyName, string[] messages) in validationError.Errors)
        {
            foreach (string message in messages)
            {
                modelState.AddModelError(propertyName, message);
            }
        }

        return ProblemDetailsFactory.CreateValidationProblemDetails(
            HttpContext,
            modelState,
            statusCode: statusCode,
            detail: validationError.Description);
    }
}
