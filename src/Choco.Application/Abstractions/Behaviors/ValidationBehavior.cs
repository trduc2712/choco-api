using Choco.SharedKernel.Results;

using FluentValidation;
using FluentValidation.Results;

using MediatR;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(cancellationToken);
        }

        ValidationContext<TRequest> context = new(request);

        ValidationResult[] validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        Dictionary<string, FieldError[]> errors = validationResults
            .SelectMany(validationResult => validationResult.Errors)
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(failure => new FieldError(
                        ValidationErrorCodes.Create(typeof(TRequest), failure.PropertyName, failure.ErrorCode),
                        failure.ErrorMessage))
                    .Distinct()
                    .ToArray());

        if (errors.Count == 0)
        {
            return await next(cancellationToken);
        }

        return FailureResult.Create<TResponse>(new ValidationError(errors));
    }
}
