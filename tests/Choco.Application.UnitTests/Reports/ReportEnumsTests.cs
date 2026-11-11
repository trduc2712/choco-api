using Choco.Application.Reports;
using Choco.Domain.Reports;

namespace Choco.Application.UnitTests.Reports;

public sealed class ReportEnumsTests
{
    [Theory]
    [InlineData("Spam", ReportReason.Spam)]
    [InlineData("HateSpeech", ReportReason.HateSpeech)]
    [InlineData("Other", ReportReason.Other)]
    public void TryParse_WithExactName_ReturnsTrueAndValue(string value, ReportReason expected)
    {
        bool parsed = ReportEnums.TryParse(value, out ReportReason result);

        parsed.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("spam")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("Spam,Other")]
    [InlineData("Unknown")]
    public void TryParse_WithAnythingElse_ReturnsFalse(string? value)
    {
        bool parsed = ReportEnums.TryParse(value, out ReportReason _);

        parsed.Should().BeFalse();
    }
}
