using AwesomeAssertions;
using Payroll.Domain;

namespace Payroll.UnitTests.Domain;

public class PayrollMonthTests
{
    [Theory]
    [InlineData("2026-03", 2026, 3, 31)]
    [InlineData("2024-02", 2024, 2, 29)]   // leap year
    [InlineData("2026-02", 2026, 2, 28)]
    [InlineData("1900-01", 1900, 1, 31)]   // lowest year
    [InlineData("9999-12", 9999, 12, 31)]  // highest year
    public void Parses_strict_YYYY_MM(string value, int year, int month, int lastDay)
    {
        PayrollMonth.TryParse(value, out var parsed).Should().BeTrue();

        parsed.FirstDay.Should().Be(new DateOnly(year, month, 1));
        parsed.LastDay.Should().Be(new DateOnly(year, month, lastDay));
        parsed.ToString().Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-3")]     // month needs two digits
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("26-03")]
    [InlineData("1899-12")]    // below year range
    [InlineData("2026/03")]
    [InlineData("2026-03-01")]
    [InlineData(" 2026-03")]
    [InlineData("2026-03 ")]
    [InlineData("abcd-ef")]
    [InlineData("-001-03")]
    [InlineData("+202-03")]
    [InlineData("２０２６-03")] // non-ASCII digits
    public void Rejects_anything_else(string? value)
    {
        PayrollMonth.TryParse(value, out var parsed).Should().BeFalse();
        parsed.Should().Be(default(PayrollMonth));
    }
}
