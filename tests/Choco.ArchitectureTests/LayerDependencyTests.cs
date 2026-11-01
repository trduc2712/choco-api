using ArchUnitNET.xUnitV3;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Choco.ArchitectureTests;

public class LayerDependencyTests : ArchitectureTestBase
{
    [Fact]
    public void Application_DoesNotDependOn_Infrastructure()
    {
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Application_DoesNotDependOn_Presentation()
    {
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Contract_DoesNotDependOn_Application()
    {
        Types().That().Are(ContractLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Contract_DoesNotDependOn_Domain()
    {
        Types().That().Are(ContractLayer)
            .Should().NotDependOnAny(DomainLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Contract_DoesNotDependOn_Infrastructure()
    {
        Types().That().Are(ContractLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Contract_DoesNotDependOn_Presentation()
    {
        Types().That().Are(ContractLayer)
            .Should().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Domain_DoesNotDependOn_Application()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Domain_DoesNotDependOn_Contract()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ContractLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Domain_DoesNotDependOn_Infrastructure()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Domain_DoesNotDependOn_Presentation()
    {
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOn_Presentation()
    {
        Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void Presentation_DoesNotDependOn_Domain()
    {
        Types().That().Are(PresentationLayer)
            .Should().NotDependOnAny(DomainLayer)
            .Check(Architecture);
    }

    [Fact]
    public void SharedKernel_DoesNotDependOn_Application()
    {
        Types().That().Are(SharedKernelLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .Check(Architecture);
    }

    [Fact]
    public void SharedKernel_DoesNotDependOn_Contract()
    {
        Types().That().Are(SharedKernelLayer)
            .Should().NotDependOnAny(ContractLayer)
            .Check(Architecture);
    }

    [Fact]
    public void SharedKernel_DoesNotDependOn_Domain()
    {
        Types().That().Are(SharedKernelLayer)
            .Should().NotDependOnAny(DomainLayer)
            .Check(Architecture);
    }

    [Fact]
    public void SharedKernel_DoesNotDependOn_Infrastructure()
    {
        Types().That().Are(SharedKernelLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .Check(Architecture);
    }

    [Fact]
    public void SharedKernel_DoesNotDependOn_Presentation()
    {
        Types().That().Are(SharedKernelLayer)
            .Should().NotDependOnAny(PresentationLayer)
            .Check(Architecture);
    }
}
