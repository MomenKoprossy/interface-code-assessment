using AwesomeAssertions;
using Payroll.Domain;
using static Payroll.UnitTests.TestData;

namespace Payroll.UnitTests.Domain;

public class MoneyRoundingTests
{
    [Theory]
    [InlineData("0.005", "0.01")]      // midpoint rounds away from zero (banker's rounding would give 0.00)
    [InlineData("2.675", "2.68")]      // exact in decimal, unlike double
    [InlineData("150.005", "150.01")]
    [InlineData("-0.005", "-0.01")]
    [InlineData("1.004", "1.00")]
    [InlineData("1.0049999", "1.00")]
    [InlineData("10.006", "10.01")]
    [InlineData("0", "0")]
    [InlineData("1234.50", "1234.50")]
    public void Rounds_to_two_decimals_half_away_from_zero(string amount, string expected) =>
        MoneyRounding.Round(Dec(amount)).Should().Be(Dec(expected));
}
