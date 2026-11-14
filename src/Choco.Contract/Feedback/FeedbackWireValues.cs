namespace Choco.Contract.Feedback;

public static class FeedbackWireValues
{
    public static readonly IReadOnlyList<string> Categories = ["bug", "suggestion", "other"];
    public static readonly IReadOnlyList<string> Statuses = ["new", "in_progress", "resolved", "wont_fix"];
    public static readonly IReadOnlyList<string> Locales = ["en", "ja", "vi"];
    public static readonly IReadOnlyList<string> Themes = ["light", "dark", "system"];
}
