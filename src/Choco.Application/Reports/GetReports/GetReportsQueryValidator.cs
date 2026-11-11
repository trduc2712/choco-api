using Choco.Contract.Reports;

using FluentValidation;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    public GetReportsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetReportsQuery.MaxPageSize);
    }
}
