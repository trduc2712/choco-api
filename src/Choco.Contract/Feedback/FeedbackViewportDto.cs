namespace Choco.Contract.Feedback;

/// <summary>The size of the browser viewport.</summary>
/// <param name="Width">The width in pixels, from 1 to 100000.</param>
/// <param name="Height">The height in pixels, from 1 to 100000.</param>
/// <example>{"width":1280,"height":720}</example>
public sealed record FeedbackViewportDto(int Width, int Height)
{
    public const int MaxSize = 100_000;
}
