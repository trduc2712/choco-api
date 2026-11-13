using Choco.Presentation.Extensions;
using Choco.Presentation.Logging;
using Choco.Presentation.Validation;
using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        TraceIds.Set(problemDetails, HttpContext);

        if (result.Error is ErrorWithExtensions errorWithExtensions)
        {
            foreach ((string key, object? value) in errorWithExtensions.Extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private ValidationProblemDetails CreateValidationProblemDetails(ValidationError validationError, int statusCode)
    {
        return ValidationProblems.Create(
            ProblemDetailsFactory,
            HttpContext,
            validationError.Errors,
            statusCode,
            validationError.Description);
    }
}
