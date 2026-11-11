using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Feedback;
using Choco.Domain.Roles;

namespace Choco.Application.Feedback.GetFeedback;

internal sealed class GetFeedbackQueryAuthorizer : IAuthorizer<GetFeedbackQuery>
{
    public PermissionRequirement GetRequirement(GetFeedbackQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyFeedback, PermissionScope.Platform);
    }
}
