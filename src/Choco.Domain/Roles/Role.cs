using Choco.Domain.Primitives.Entities;

namespace Choco.Domain.Roles;

public sealed class Role : AggregateRoot, IAuditable
{
    public const int NameMaxLength = 64;
    public const int SystemKeyMaxLength = 64;

    private readonly List<string> _permissions = [];

    public string Name { get; private set; } = null!;
    public string? SystemKey { get; private set; }
    public bool IsDefault { get; private set; }
    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ModifiedAt { get; private set; }

    private Role() { }

    private Role(Guid id, string name, string? systemKey, bool isDefault, IEnumerable<string> permissions)
        : base(id)
    {
        Name = name;
        SystemKey = systemKey;
        IsDefault = isDefault;
        _permissions.AddRange(permissions);
    }

    public static Role CreateSystem(SystemRoleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Key, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Key.Length, SystemKeyMaxLength, nameof(definition));
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name, nameof(definition));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Name.Length, NameMaxLength, nameof(definition));

        string? unknownPermission = definition.Permissions.FirstOrDefault(permission => !Permission.All.Contains(permission));

        if (unknownPermission is not null)
        {
            throw new ArgumentException($"Unknown permission '{unknownPermission}'.", nameof(definition));
        }

        return new Role(
            Guid.NewGuid(),
            definition.Name,
            definition.Key,
            definition.IsDefault,
            definition.Permissions.Distinct(StringComparer.Ordinal));
    }

    public bool IsCoveredBy(IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        return _permissions.All(permissions.Contains);
    }
}
