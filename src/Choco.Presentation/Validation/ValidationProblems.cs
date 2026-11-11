using System.Text.Json;

using Choco.SharedKernel.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Choco.Presentation.Validation;

internal static class ValidationProblems
{
    public const string ErrorCodesKey = "errorCodes";
    public const string RequestResource = "Request";
    private const string DefaultMessage = "The input was not valid.";
    private const string BodyField = "Body";

    public static ValidationProblemDetails Create(
        ProblemDetailsFactory factory,
        HttpContext httpContext,
        IReadOnlyDictionary<string, FieldError[]> errors,
        int statusCode,
        string? detail)
    {
        ModelStateDictionary modelState = new();
        Dictionary<string, List<string>> codes = [];

        foreach ((string path, FieldError[] fieldErrors) in errors)
        {
            string field = ToJsonName(path);

            if (!codes.TryGetValue(field, out List<string>? fieldCodes))
            {
                fieldCodes = [];
                codes[field] = fieldCodes;
            }

            foreach (FieldError fieldError in fieldErrors)
            {
                modelState.AddModelError(field, fieldError.Message);
                fieldCodes.Add(fieldError.Code);
            }
        }

        ValidationProblemDetails problem = factory.CreateValidationProblemDetails(
            httpContext, modelState, statusCode: statusCode, detail: detail);
        problem.Extensions[ErrorCodesKey] = codes.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

        return problem;
    }

    public static Dictionary<string, FieldError[]> FromModelState(
        ModelStateDictionary modelState, IReadOnlyCollection<string> bodyParameterNames)
    {
        Dictionary<string, FieldError[]> errors = [];

        foreach ((string key, ModelStateEntry entry) in modelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            string path = bodyParameterNames.Contains(key, StringComparer.OrdinalIgnoreCase)
                ? "body"
                : NormalizeModelStateKey(key);
            string pascalPath = ToPascalPath(path);

            errors[path] = entry.Errors
                .Select(error =>
                {
                    string message = string.IsNullOrEmpty(error.ErrorMessage) ? DefaultMessage : error.ErrorMessage;
                    string reason = message.Contains("is required", StringComparison.Ordinal) ? "Required" : "Invalid";

                    return new FieldError($"{RequestResource}.{pascalPath}.{reason}", message);
                })
                .Distinct()
                .ToArray();
        }

        return errors;
    }

    public static string ToJsonName(string path)
    {
        return string.Join('.', path.Split('.').Select(ConvertSegment));

        static string ConvertSegment(string segment)
        {
            int indexStart = segment.IndexOf('[', StringComparison.Ordinal);

            return indexStart < 0
                ? JsonNamingPolicy.CamelCase.ConvertName(segment)
                : JsonNamingPolicy.CamelCase.ConvertName(segment[..indexStart]) + segment[indexStart..];
        }
    }

    private static string NormalizeModelStateKey(string key)
    {
        string path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;

        return path is "" or "$" ? "body" : path;
    }

    private static string ToPascalPath(string path)
    {
        return string.Join('.', path.Split('.').Select(segment =>
        {
            string name = segment.Contains('[', StringComparison.Ordinal) ? segment[..segment.IndexOf('[', StringComparison.Ordinal)] : segment;

            return name.Length == 0 ? BodyField : char.ToUpperInvariant(name[0]) + name[1..];
        }));
    }
}
