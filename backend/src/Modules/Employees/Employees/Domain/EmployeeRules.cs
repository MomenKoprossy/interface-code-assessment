using System.Text.RegularExpressions;

namespace Employees.Domain;

/// <summary>
/// The single source of the employee rules (limits + predicates, no messages). The application validator turns
/// them into field-level messages for the API; the <see cref="Employee"/> aggregate re-checks them as guards so no
/// caller (seeder, another module, a test) can build an invalid employee; the EF mapping uses the same limits.
/// </summary>
public static partial class EmployeeRules
{
    public const int MaxTextLength = 100;
    public const int MaxEmailLength = 254;
    public const int MaxPayItemNameLength = 50;
    public const int MaxPayItems = 20;
    public const decimal MaxAmount = 1_000_000m;
    public static readonly DateOnly MinHireDate = new(1900, 1, 1);

    /// <summary>Not blank, and at most <paramref name="maxLength"/> characters once trimmed (the stored value).</summary>
    public static bool IsValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    public static bool IsValidEmailFormat(string email) => EmailFormat().IsMatch(email.Trim());

    public static bool IsValidEmail(string? email) =>
        IsValidText(email, MaxEmailLength) && IsValidEmailFormat(email!);

    public static bool HasAtMostTwoDecimals(decimal amount) => decimal.Round(amount, 2) == amount;

    /// <summary>Salary and pay-item amounts: &gt; 0, ≤ <see cref="MaxAmount"/>, at most 2 decimals.</summary>
    public static bool IsValidMoney(decimal amount) =>
        amount > 0m && amount <= MaxAmount && HasAtMostTwoDecimals(amount);

    /// <summary>Hire date between <see cref="MinHireDate"/> and <paramref name="today"/> (UTC) inclusive.</summary>
    public static bool IsValidHireDate(DateOnly hireDate, DateOnly today) =>
        hireDate >= MinHireDate && hireDate <= today;

    /// <summary>Pay-item names compare trimmed and case-insensitively.</summary>
    public static string PayItemNameKey(string name) => name.Trim().ToUpperInvariant();

    public static bool IsValidPayItem(PayItem item) =>
        IsValidText(item.Name, MaxPayItemNameLength) && IsValidMoney(item.Amount);

    public static bool IsValidPayItemList(IReadOnlyCollection<PayItem> items) =>
        items.Count <= MaxPayItems
        && items.All(IsValidPayItem)
        && items.Select(i => PayItemNameKey(i.Name)).Distinct().Count() == items.Count;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex EmailFormat();
}
