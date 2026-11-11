using Choco.Contract.Abstractions;
using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Reports;

/// <summary>Lists the open reports, oldest first.</summary>
/// <param name="Page">The page number, from 1. Defaults to 1 at the endpoint.</param>
/// <param name="PageSize">The page size, from 1 to 50. Defaults to 20 at the endpoint.</param>
/// <example>{ "page": 1, "pageSize": 20 }</example>
public sealed record GetReportsQuery(int Page, int PageSize) : IQuery<OffsetPageResponse<ReportResponse>>
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}
