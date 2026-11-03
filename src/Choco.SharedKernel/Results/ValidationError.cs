namespace Choco.SharedKernel.Results;

public sealed record ValidationError : Error
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationError(IReadOnlyDictionary<string, string[]> errors)
        : base("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation)
    {
        Errors = errors;
    }
}
