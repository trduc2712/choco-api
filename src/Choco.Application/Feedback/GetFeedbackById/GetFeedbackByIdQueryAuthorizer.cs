using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Feedback;
using Choco.Domain.Roles;

namespace Choco.Application.Feedback.GetFeedbackById;

internal sealed class GetFeedbackByIdQueryAuthorizer : IAuthorizer<GetFeedbackByIdQuery>
{
    public PermissionRequirement GetRequirement(GetFeedbackByIdQuery request)
    {
        return new PermissionRequirement(Permission.ReadAnyFeedback, PermissionScope.Platform);
    }
}
