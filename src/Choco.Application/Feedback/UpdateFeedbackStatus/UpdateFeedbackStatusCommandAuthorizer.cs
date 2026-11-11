using Choco.Application.Abstractions.Authorization;
using Choco.Contract.Feedback;
using Choco.Domain.Roles;

namespace Choco.Application.Feedback.UpdateFeedbackStatus;

internal sealed class UpdateFeedbackStatusCommandAuthorizer : IAuthorizer<UpdateFeedbackStatusCommand>
{
    public PermissionRequirement GetRequirement(UpdateFeedbackStatusCommand request)
    {
        return new PermissionRequirement(Permission.ChangeFeedbackStatus, PermissionScope.Platform);
    }
}
