using Choco.Domain.Primitives.Entities;
using Choco.SharedKernel.Results;

namespace Choco.Domain.Roles;

public sealed class Role : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 64;
    public const int SystemKeyMaxLength = 64;
    public const int DescriptionMaxLength = 200;
    public const int AppearanceKeyMaxLength = 16;

    private readonly List<string> _permissions = [];

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Color { get; private set; } = null!;
    public string Icon { get; private set; } = null!;
    public string? SystemKey { get; private set; }
    public bool IsDefault { get; private set; }
    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    private Role() { }

    private Role(
        Guid id, string name, string? systemKey, bool isDefault, IEnumerable<string> permissions,
        string? description, string color, string icon, Guid? modifiedBy)
        : base(id)
    {
        Name = name;
        SystemKey = systemKey;
        IsDefault = isDefault;
        ModifiedBy = modifiedBy;
        ApplyAppearance(description, color, icon);
        _permissions.AddRange(permissions);
    }

    public static Role CreateSystem(SystemRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Key, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Key.Length, SystemKeyMaxLength, nameof(definition));
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Name.Length, NameMaxLength, nameof(definition));

        EnsureKnown(definition.Permissions, nameof(definition));

        return new Role(
            Guid.NewGuid(),
            definition.Name,
            definition.Key,
            definition.IsDefault,
            definition.Permissions.Distinct(StringComparer.Ordinal),
            definition.Description,
            definition.Color,
            definition.Icon,
            null);
    }

    public static Role Create(
        string name, IEnumerable<string> permissions,
        string? description = null, string color = RoleColors.Gray, string icon = RoleIcons.Shield, Guid? modifiedBy = null)
    {
        string trimmedName = Normalize(name, permissions);

        return new Role(
            Guid.NewGuid(), trimmedName, null, false, permissions.Distinct(StringComparer.Ordinal), description, color, icon, modifiedBy);
    }

    public void Update(string name, IEnumerable<string> permissions) =>
        Update(name, permissions, Description, Color, Icon, ModifiedBy);

    public void Update(
        string name, IEnumerable<string> permissions, string? description, string color, string icon, Guid? modifiedBy)
    {
        string trimmedName = Normalize(name, permissions);
        List<string> distinctPermissions = permissions.Distinct(StringComparer.Ordinal).ToList();

        ApplyAppearance(description, color, icon);
        Name = trimmedName;
        ModifiedBy = modifiedBy;
        _permissions.Clear();
        _permissions.AddRange(distinctPermissions);
    }

    public Result CheckCanBeChangedBy(
        IReadOnlyCollection<string> actorPermissions, bool actorHoldsRole, IEnumerable<string> newPermissions)
    {
        ArgumentNullException.ThrowIfNull(actorPermissions);
        ArgumentNullException.ThrowIfNull(newPermissions);

        if (actorHoldsRole)
        {
            return Result.Failure(RoleErrors.HeldByCaller);
        }

        if (!IsCoveredBy(actorPermissions) || !newPermissions.All(actorPermissions.Contains))
        {
            return Result.Failure(PermissionErrors.Denied);
        }

        return Result.Success();
    }

    public Result CheckCanBeDeleted()
    {
        if (IsDefault)
        {
            return Result.Failure(RoleErrors.DefaultNotDeletable);
        }

        return SystemKey is null ? Result.Success() : Result.Failure(RoleErrors.SystemNotDeletable);
    }

    public bool IsCoveredBy(IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return _permissions.All(permissions.Contains);
    }

    private void ApplyAppearance(string? description, string color, string icon)
    {
        string? trimmedDescription = description?.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedDescription?.Length ?? 0, DescriptionMaxLength, nameof(description));

        if (!RoleColors.All.Contains(color))
        {
            throw new ArgumentException("Unknown role color.", nameof(color));
        }

        if (!RoleIcons.All.Contains(icon))
        {
            throw new ArgumentException("Unknown role icon.", nameof(icon));
        }

        Description = string.IsNullOrEmpty(trimmedDescription) ? null : trimmedDescription;
        Color = color;
        Icon = icon;
    }

    private static string Normalize(string name, IEnumerable<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);

        string trimmedName = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmedName.Length, NameMaxLength, nameof(name));
        EnsureKnown(permissions, nameof(permissions));

        return trimmedName;
    }

    private static void EnsureKnown(IEnumerable<string> permissions, string paramName)
    {
        string? unknownPermission = permissions.FirstOrDefault(permission => !Permission.All.Contains(permission));

        if (unknownPermission is not null)
        {
            throw new ArgumentException($"Unknown permission '{unknownPermission}'.", paramName);
        }
    }
}
