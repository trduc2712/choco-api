using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Application.Abstractions.Authentication;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Infrastructure.Authentication;

internal sealed class ClerkIdentityProvider : IIdentityProvider
{
    private const string UsersPath = "users/";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;

    public ClerkIdentityProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IdpUser?> GetUserAsync(string identityId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            UsersPath + Uri.EscapeDataString(identityId),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await ReadUserAsync(response, cancellationToken);
    }

    public async Task<Result<IdpUser>> UpdateProfileAsync(
        string identityId, string username, string firstName, string lastName, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.PatchAsJsonAsync(
            UsersPath + Uri.EscapeDataString(identityId),
            new { username, firstName, lastName },
            SerializerOptions,
            cancellationToken);

        Error? error = await GetProfileErrorAsync(response, "Username", cancellationToken);
        if (error is not null)
        {
            return Result<IdpUser>.Failure(error);
        }

        IdpUser user = await ReadUserAsync(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(user.Username))
        {
            throw new InvalidOperationException("The identity provider returned a profile without a username.");
        }

        return Result<IdpUser>.Success(user);
    }

    public async Task<Result<IdpUser>> UploadAvatarAsync(string identityId, Stream image, CancellationToken cancellationToken)
    {
        using MultipartFormDataContent body = new();
        StreamContent file = new(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        body.Add(file, "file", "avatar");
        using HttpResponseMessage response = await _httpClient.PostAsync(ProfileImagePath(identityId), body, cancellationToken);

        Error? error = await GetProfileErrorAsync(response, "Image", cancellationToken);
        return error is null
            ? Result<IdpUser>.Success(await ReadUserAsync(response, cancellationToken))
            : Result<IdpUser>.Failure(error);
    }

    public async Task<Result> RemoveAvatarAsync(string identityId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.DeleteAsync(ProfileImagePath(identityId), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            ClerkErrors? errors = response.Content.Headers.ContentLength == 0
                ? null
                : await response.Content.ReadFromJsonAsync<ClerkErrors>(SerializerOptions, cancellationToken);
            return errors?.Errors?.Any(error => error.Code == "image_not_found") == true
                ? Result.Success()
                : Result.Failure(UserErrors.Deleted);
        }

        response.EnsureSuccessStatusCode();
        return Result.Success();
    }

    private static string ProfileImagePath(string identityId) =>
        UsersPath + Uri.EscapeDataString(identityId) + "/profile_image";

    private static async Task<Error?> GetProfileErrorAsync(
        HttpResponseMessage response, string defaultField, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return UserErrors.Deleted;
        }

        if (response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.RequestEntityTooLarge))
        {
            response.EnsureSuccessStatusCode();
            return null;
        }

        ClerkErrors? body = await response.Content.ReadFromJsonAsync<ClerkErrors>(SerializerOptions, cancellationToken);
        if (body?.Errors?.Any(error => error.Code == "form_identifier_exists" && error.Meta?.ParamName == "username") == true)
        {
            return UserErrors.UsernameTaken;
        }

        var errors = new Dictionary<string, FieldError[]>();
        foreach (ClerkError error in body?.Errors ?? [])
        {
            string field = error.Meta?.ParamName switch
            {
                "username" => "Username",
                "first_name" => "FirstName",
                "last_name" => "LastName",
                _ => defaultField
            };
            errors[field] = [Rejected(field)];
        }

        if (errors.Count == 0)
        {
            errors[defaultField] = [Rejected(defaultField)];
        }

        return new ValidationError(errors);
    }

    private static FieldError Rejected(string field)
    {
        return new FieldError($"User.{field}.Rejected", "The value was rejected by the identity provider.");
    }

    private static async Task<IdpUser> ReadUserAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ClerkUserResponse clerkUser = (await response.Content.ReadFromJsonAsync<ClerkUserResponse>(SerializerOptions, cancellationToken))!;
        string fullName = string.Join(' ', new[] { clerkUser.FirstName, clerkUser.LastName }.Where(name => !string.IsNullOrWhiteSpace(name)));

        return new IdpUser(clerkUser.Id, clerkUser.Username, fullName.Length > 0 ? fullName : null, clerkUser.ImageUrl);
    }

    private sealed record ClerkErrors(ClerkError[]? Errors);
    private sealed record ClerkError(string Code, ClerkErrorMeta? Meta);
    private sealed record ClerkErrorMeta(string? ParamName);
    private sealed record ClerkUserResponse(string Id, string? Username, string? FirstName, string? LastName, string? ImageUrl);
}
