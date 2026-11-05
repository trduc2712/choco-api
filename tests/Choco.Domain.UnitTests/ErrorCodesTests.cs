using System.Reflection;

using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests;

public sealed class ErrorCodesTests
{
    [Fact]
    public void Codes_AcrossErrorClasses_AreUnique()
    {
        IEnumerable<string> codes = AssemblyReference.Assembly.GetTypes()
            .Where(type => type.IsAbstract && type.IsSealed && type.Name.EndsWith("Errors", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => ((Error)field.GetValue(null)!).Code);

        codes.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
    }
}
