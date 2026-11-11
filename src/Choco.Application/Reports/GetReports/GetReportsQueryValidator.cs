using Choco.Contract.Reports;

using FluentValidation;

namespace Choco.Application.Reports.GetReports;

internal sealed class GetReportsQueryValidator : AbstractValidator<GetReportsQuery>
{
    public GetReportsQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetReportsQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => ReportCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
