using System.Text.Json.Serialization;

using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Feedback;

/// <summary>Sends feedback about the app.</summary>
/// <param name="Category">What kind of feedback it is: bug, suggestion or other.</param>
/// <param name="Message">What the user wants to say, from 10 to 2000 characters after trimming.</param>
/// <param name="Context">Where and how the user was using the app.</param>
/// <param name="UserAgent">The browser's user agent; the endpoint sets it from the User-Agent header.</param>
/// <example>{"category":"bug","message":"message of the feedback","context":{"path":"/posts/123","locale":"en","theme":"system","viewport":{"width":1280,"height":720},"appVersion":"app-version"}}</example>
public sealed record CreateFeedbackCommand(
    string Category,
    string Message,
    CreateFeedbackContextDto Context,
    [property: JsonIgnore] string UserAgent = "") : ICommand<CreatedFeedbackResponse>;
