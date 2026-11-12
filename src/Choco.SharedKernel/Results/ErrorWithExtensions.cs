namespace Choco.SharedKernel.Results;

public sealed record ErrorWithExtensions : Error
{
    public IReadOnlyDictionary<string, object?> Extensions { get; }

    public ErrorWithExtensions(
        string code, string description, ErrorType type, IReadOnlyDictionary<string, object?> extensions)
        : base(code, description, type)
    {
        Extensions = extensions;
    }
}
