namespace Choco.SharedKernel.Results;

public sealed record ValidationError : Error
{
    public IReadOnlyDictionary<string, FieldError[]> Errors { get; }

    public ValidationError(IReadOnlyDictionary<string, FieldError[]> errors)
        : base("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation)
    {
        Errors = errors;
    }
}
