namespace Choco.ArchitectureTests;

public class ArchitectureSetupTests : ArchitectureTestBase
{
    private const string ChocoAssemblySearchPattern = "Choco.*.dll";
    private const string TestAssemblySuffix = "Tests";

    private static readonly HashSet<string> IgnoredAssemblies = [];

    [Fact]
    public void Architecture_Loads_AllChocoAssemblies()
    {
        HashSet<string?> loadedAssemblies = LoadedAssemblies
            .Select(a => a.GetName().Name)
            .ToHashSet();

        // Scan the output folder instead of GetReferencedAssemblies(): the compiler
        // drops project references whose types are never used, so a new project
        // would be invisible there. Project references are always copied to bin.
        IReadOnlyList<string> missingAssemblies = Directory
            .GetFiles(AppContext.BaseDirectory, ChocoAssemblySearchPattern)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => !name.EndsWith(TestAssemblySuffix, StringComparison.Ordinal))
            .Where(name => !IgnoredAssemblies.Contains(name))
            .Where(name => !loadedAssemblies.Contains(name))
            .Order()
            .ToList();

        Assert.True(
            missingAssemblies.Count == 0,
            $"Not loaded into Architecture: {string.Join(", ", missingAssemblies)}. " +
            "Add them to ArchitectureTestBase.LoadedAssemblies or to IgnoredAssemblies.");
    }
}
