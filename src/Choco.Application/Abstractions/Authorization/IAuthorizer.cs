namespace Choco.Application.Abstractions.Authorization;

public interface IAuthorizer<in TRequest>
{
    PermissionRequirement GetRequirement(TRequest request);
}
