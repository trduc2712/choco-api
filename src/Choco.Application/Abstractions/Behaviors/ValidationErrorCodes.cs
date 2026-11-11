using System.Text.RegularExpressions;

namespace Choco.Application.Abstractions.Behaviors;

internal static partial class ValidationErrorCodes
{
    private const string ContractNamespacePrefix = "Choco.Contract.";
    private const string FallbackResource = "Request";
    private const string ValidatorSuffix = "Validator";

    public static string Create(Type requestType, string propertyName, string? failureCode)
    {
        string field = IndexPattern().Replace(propertyName, string.Empty);

        return $"{ResourceOf(requestType)}.{field}.{ReasonOf(failureCode)}";
    }

    public static string ResourceOf(Type requestType)
    {
        string? @namespace = requestType.Namespace;

        if (@namespace is null || !@namespace.StartsWith(ContractNamespacePrefix, StringComparison.Ordinal))
        {
            return FallbackResource;
        }

        string folder = @namespace[ContractNamespacePrefix.Length..].Split('.')[0];

        return folder.Length > 1 && folder.EndsWith('s') ? folder[..^1] : folder;
    }

    public static string ReasonOf(string? failureCode)
    {
        if (string.IsNullOrEmpty(failureCode))
        {
            return "Invalid";
        }

        if (!failureCode.EndsWith(ValidatorSuffix, StringComparison.Ordinal))
        {
            return failureCode;
        }

        return failureCode switch
        {
            "NotNullValidator" or "NotEmptyValidator" => "Required",
            "MaximumLengthValidator" => "TooLong",
            "MinimumLengthValidator" => "TooShort",
            "InclusiveBetweenValidator"
                or "ExclusiveBetweenValidator"
                or "GreaterThanValidator"
                or "GreaterThanOrEqualValidator"
                or "LessThanValidator"
                or "LessThanOrEqualValidator" => "OutOfRange",
            "PredicateValidator" or "AsyncPredicateValidator" => "Invalid",
            _ => failureCode[..^ValidatorSuffix.Length]
        };
    }

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex IndexPattern();
}
