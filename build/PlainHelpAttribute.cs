using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Nuke.Common;
using Nuke.Common.Execution;

[AttributeUsage(AttributeTargets.Class)]
class PlainHelpAttribute : BuildExtensionAttributeBase, IOnBuildInitialized
{
    public override float Priority { get; set; } = 6;

    public void OnBuildInitialized(
        IReadOnlyCollection<ExecutableTarget> executableTargets,
        IReadOnlyCollection<ExecutableTarget> executionPlan)
    {
        if (!Console.IsOutputRedirected || !(Build.Help || executionPlan.Count == 0))
            return;

        var text = new StringBuilder();
        text.AppendLine("Targets (with their direct dependencies):");
        foreach (var target in executableTargets.Where(x => x.Listed))
        {
            var name = target.Name + (target.IsDefault ? " (default)" : "");
            var dependencies = target.ExecutionDependencies.Count > 0
                ? " -> " + string.Join(", ", target.ExecutionDependencies.Select(x => x.Name))
                : "";
            text.AppendLine($"  {name,-22}{dependencies}");
            if (!string.IsNullOrWhiteSpace(target.Description))
                text.AppendLine($"      {target.Description}");
        }

        var defaultTargets = string.Join(", ", executableTargets.Where(x => x.IsDefault).Select(x => x.Name));

        text.AppendLine();
        text.AppendLine("Parameters:");
        foreach (var (member, parameter) in GetParameters(Build.GetType()))
        {
            var description = parameter.Description?.Replace("{default_target}", defaultTargets) ?? "<no description>";
            text.AppendLine($"  --{ToDashedName(parameter.Name ?? member.Name),-20}{description}");
        }

        Console.Out.Write(text.ToString());
        Environment.Exit(0);
    }

    static IEnumerable<(MemberInfo Member, ParameterAttribute Parameter)> GetParameters(Type? type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(flags))
            {
                if (member.GetCustomAttribute<ParameterAttribute>() is { } parameter)
                    yield return (member, parameter);
            }
        }
    }

    static string ToDashedName(string name) =>
        Regex.Replace(name, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
}
