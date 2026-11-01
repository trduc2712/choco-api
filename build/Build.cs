using System.Text.RegularExpressions;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;

using static Nuke.Common.Tools.DotNet.DotNetTasks;

[PlainHelp]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    public enum BuildConfiguration { Debug, Release }

    public enum TestKind { Unit, Architecture, Integration }

    [Parameter("Debug | Release. Default: Debug locally, Release on CI")]
    readonly BuildConfiguration Configuration = IsLocalBuild ? BuildConfiguration.Debug : BuildConfiguration.Release;

    [Parameter("Test kinds for Test: unit | architecture | integration, several allowed")] readonly TestKind[] TestType = [];
    [Parameter("xUnit class filter for Test, wildcards allowed, e.g. *MoneyTests")] readonly string? TestClass;
    [Parameter("xUnit method filter for Test, wildcards allowed, e.g. *Should_fail*")] readonly string? TestMethod;
    [Parameter("Migration name for MigrationAdd, e.g. AddProducts")] readonly string? MigrationName;

    [PathVariable] readonly Tool Docker = null!;

    AbsolutePath SolutionFile => RootDirectory / "Choco.slnx";
    AbsolutePath ComposeFile => RootDirectory / "compose.yaml";
    AbsolutePath ApiProject => RootDirectory / "Choco.Presentation";
    AbsolutePath InfrastructureProject => RootDirectory / "Choco.Infrastructure";
    AbsolutePath MigrationsDirectory => InfrastructureProject / "Persistence" / "Migrations";
    AbsolutePath TestResultsDirectory => RootDirectory / "artifacts" / "test-results";

    Target Restore => _ => _
        .Description("Restore NuGet packages and local dotnet tools")
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SolutionFile));
            DotNetToolRestore(s => s.SetProcessWorkingDirectory(RootDirectory));
        });

    Target Compile => _ => _
        .Description("Build the solution")
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration.ToString())
            .EnableNoRestore()));

    Target Test => _ => _
        .Description("Run tests; narrow with --test-type, --test-class, --test-method")
        .DependsOn(Compile)
        .Executes(() =>
        {
            IReadOnlyCollection<AbsolutePath> targets = TestType.Length == 0
                ? [SolutionFile]
                : TestType.SelectMany(kind => RootDirectory.GlobFiles($"tests/*.{kind}Tests/*.csproj")).ToList();
            Assert.NotEmpty(targets, $"No test projects found for test types '{string.Join(", ", TestType)}'");

            var report = IsServerBuild ? $"--report-xunit-trx --results-directory {TestResultsDirectory}" : "";
            var filter = TestFilterArguments();
            if (filter.Length == 0)
            {
                foreach (var target in targets)
                    DotNet($"test --project {target} --configuration {Configuration} --no-build {report:nq}");
                return;
            }

            var output = targets
                .SelectMany(target => DotNet(
                    $"test --project {target} --configuration {Configuration} --no-build --ignore-exit-code 8 {report:nq} {filter:nq}"))
                .ToList();
            Assert.True(
                output.Any(x => Regex.IsMatch(x.Text, @"succeeded: [1-9]")),
                $"No tests matched the filter: {filter}");
        });

    Target Format => _ => _
        .Description("Format code in place")
        .DependsOn(Restore)
        .Executes(() => DotNetFormat(s => s
            .SetProject(SolutionFile)
            .AddExclude(RootDirectory.GetRelativePathTo(MigrationsDirectory))
            .SetProcessWorkingDirectory(RootDirectory)
            .EnableNoRestore()));

    Target FormatCheck => _ => _
        .Description("Fail if code is not formatted")
        .DependsOn(Restore)
        .Executes(() => DotNetFormat(s => s
            .SetProject(SolutionFile)
            .AddExclude(RootDirectory.GetRelativePathTo(MigrationsDirectory))
            .SetProcessWorkingDirectory(RootDirectory)
            .EnableNoRestore()
            .EnableVerifyNoChanges()));

    Target Check => _ => _
        .Description("FormatCheck + Compile + Test; run before finishing work")
        .DependsOn(FormatCheck, Test);

    Target Run => _ => _
        .Description("Run the API on https://localhost:7000 and http://localhost:5000")
        .DependsOn(Compile)
        .Executes(() => DotNetRun(s => s
            .SetProjectFile(ApiProject)
            .SetConfiguration(Configuration.ToString())
            .SetLaunchProfile("https")
            .EnableNoBuild()));

    Target Watch => _ => _
        .Description("Run the API with hot reload")
        .Executes(() => DotNet($"watch --project {ApiProject} run"));

    Target DbUp => _ => _
        .Description("Start Postgres in Docker and wait until it is healthy")
        .Executes(() => Docker($"compose -f {ComposeFile} up -d --wait postgres"));

    Target DbDown => _ => _
        .Description("Stop Postgres container (data volume is kept)")
        .Executes(() => Docker($"compose -f {ComposeFile} down"));

    Target MigrationAdd => _ => _
        .Description("Add an EF Core migration; requires --migration-name")
        .DependsOn(Compile)
        .Requires(() => MigrationName)
        .Executes(() => Ef($"migrations add {MigrationName} --output-dir Persistence/Migrations"));

    Target MigrationApply => _ => _
        .Description("Apply pending migrations to the database")
        .DependsOn(Compile)
        .Executes(() => Ef("database update"));

    Target DbReset => _ => _
        .Description("DESTRUCTIVE: drop the database and re-apply all migrations; local, interactive only")
        .DependsOn(Compile)
        .OnlyWhenStatic(() => IsLocalBuild)
        .Executes(() =>
        {
            Assert.False(Console.IsInputRedirected, "DbReset must be run interactively from a terminal");
            Console.Write("Type 'reset' to drop the local database: ");
            Assert.True(Console.ReadLine() == "reset", "DbReset cancelled");

            Ef("database drop --force");
            Ef("database update");
        });

    string TestFilterArguments() => string.Join(' ',
        new[] { ("--filter-class", TestClass), ("--filter-method", TestMethod) }
            .Where(x => x.Item2 is not null)
            .Select(x => $"{x.Item1} \"{x.Item2}\""));

    void Ef(string command) => DotNet(
        $"ef {command:nq} --project {InfrastructureProject} --startup-project {ApiProject} --configuration {Configuration} --no-build",
        RootDirectory);
}
