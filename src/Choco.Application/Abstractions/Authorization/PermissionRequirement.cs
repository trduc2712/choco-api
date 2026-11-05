using Choco.Domain.Roles;

namespace Choco.Application.Abstractions.Authorization;

public sealed record PermissionRequirement(string Permission, PermissionScope Scope);
