namespace Choco.Contract.Reports;

/// <summary>How many open reports about a target give one reason.</summary>
/// <param name="Reason">The reason: Spam, Harassment, HateSpeech, Violence or Other.</param>
/// <param name="Count">The number of open reports with this reason.</param>
/// <example>{"reason":"Spam","count":3}</example>
public sealed record ReportReasonCountResponse(string Reason, int Count);
