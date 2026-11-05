namespace Choco.Application.Abstractions.Authorization;

public sealed class AdministratorBootstrapOptions
{
    public const string SectionName = "Authorization";

    public List<string> BootstrapAdminIdentityIds { get; set; } = [];
}
