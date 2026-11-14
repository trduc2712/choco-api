namespace Choco.Domain.Reports;

public sealed record ReportSearch(string Phrase, IReadOnlyList<string> Words);
