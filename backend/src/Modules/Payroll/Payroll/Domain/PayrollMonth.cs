namespace Payroll.Domain;

/// <summary>A calendar month in the strict <c>YYYY-MM</c> format, year 1900-9999.</summary>
public readonly record struct PayrollMonth
{
    public const string FormatMessage = "Month must be a valid month in YYYY-MM format (year 1900-9999).";

    private PayrollMonth(int year, int month) => FirstDay = new DateOnly(year, month, 1);

    public DateOnly FirstDay { get; }

    public DateOnly LastDay => FirstDay.AddDays(DateTime.DaysInMonth(FirstDay.Year, FirstDay.Month) - 1);

    public static bool TryParse(string? value, out PayrollMonth month)
    {
        month = default;
        if (value is not { Length: 7 } || value[4] != '-'
            || value.AsSpan(0, 4).ContainsAnyExceptInRange('0', '9')
            || value.AsSpan(5, 2).ContainsAnyExceptInRange('0', '9'))
            return false;

        var year = int.Parse(value.AsSpan(0, 4));
        var monthNumber = int.Parse(value.AsSpan(5, 2));
        if (year is < 1900 or > 9999 || monthNumber is < 1 or > 12)
            return false;

        month = new PayrollMonth(year, monthNumber);
        return true;
    }

    public override string ToString() => FirstDay.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
