using Choco.Application.Content;
using Choco.Domain.Reports;

namespace Choco.Application.Reports;

internal static class ReportSearches
{
    public static ReportSearch? Create(string? text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new ReportSearch(text.Trim(), SearchTerms.Split(text, maxWords));
    }
}
