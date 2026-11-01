using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

using ApplicationAssemblyReference = Choco.Application.AssemblyReference;
using ArchUnitArchitecture = ArchUnitNET.Domain.Architecture;
using Assembly = System.Reflection.Assembly;
using ContractAssemblyReference = Choco.Contract.AssemblyReference;
using DomainAssemblyReference = Choco.Domain.AssemblyReference;
using InfrastructureAssemblyReference = Choco.Infrastructure.AssemblyReference;
using PresentationAssemblyReference = Choco.Presentation.AssemblyReference;
using SharedKernelAssemblyReference = Choco.SharedKernel.AssemblyReference;

namespace Choco.ArchitectureTests;

/// <summary>
/// Inherit to get the loaded <see cref="Architecture"/> and layer definitions.
/// </summary>
/// <remarks>
/// When adding a project: add its assembly to <see cref="LoadedAssemblies"/>, define its
/// layer here, and add its rules to <c>LayerDependencyTests</c>. A layer whose assembly
/// is not loaded is empty, so its rules pass silently; <c>ArchitectureSetupTests</c>
/// fails if an assembly is missing.
/// <para>
/// Field order matters: static initializers run top to bottom.
/// </para>
/// </remarks>
public abstract class ArchitectureTestBase
{
    private static readonly Assembly ApplicationAssembly = ApplicationAssemblyReference.Assembly;
    private static readonly Assembly ContractAssembly = ContractAssemblyReference.Assembly;
    private static readonly Assembly DomainAssembly = DomainAssemblyReference.Assembly;
    private static readonly Assembly InfrastructureAssembly = InfrastructureAssemblyReference.Assembly;
    private static readonly Assembly PresentationAssembly = PresentationAssemblyReference.Assembly;
    private static readonly Assembly SharedKernelAssembly = SharedKernelAssemblyReference.Assembly;

    protected static readonly IReadOnlyList<Assembly> LoadedAssemblies =
    [
        ApplicationAssembly,
        ContractAssembly,
        DomainAssembly,
        InfrastructureAssembly,
        PresentationAssembly,
        SharedKernelAssembly,
    ];

    protected static readonly ArchUnitArchitecture Architecture = new ArchLoader()
        .LoadAssemblies(LoadedAssemblies.ToArray())
        .Build();

    protected static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application Layer");

    protected static readonly IObjectProvider<IType> ContractLayer =
        Types().That().ResideInAssembly(ContractAssembly).As("Contract Layer");

    protected static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain Layer");

    protected static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure Layer");

    protected static readonly IObjectProvider<IType> PresentationLayer =
        Types().That().ResideInAssembly(PresentationAssembly).As("Presentation Layer");

    protected static readonly IObjectProvider<IType> SharedKernelLayer =
        Types().That().ResideInAssembly(SharedKernelAssembly).As("SharedKernel Layer");
}
