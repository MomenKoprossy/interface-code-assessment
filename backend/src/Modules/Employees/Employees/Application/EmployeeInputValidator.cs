using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BuildingBlocks.Abstractions;
using Employees.Domain;
using FluentValidation;

namespace Employees.Application;

/// <summary>
/// The validation rules table of the API contract, shared by create and update. Text lengths are checked after
/// trimming, because the trimmed value is what gets stored.
/// </summary>
internal abstract partial class EmployeeInputValidator<T> : AbstractValidator<T>
    where T : EmployeeInput
{
    public const int MaxPayItems = 20;
    public static readonly DateOnly MinHireDate = new(1900, 1, 1);

    protected EmployeeInputValidator(IClock clock)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FullName).RequiredText("Full name", EmployeeLimits.MaxTextLength);
        RuleFor(x => x.JobTitle).RequiredText("Job title", EmployeeLimits.MaxTextLength);
        RuleFor(x => x.Department).RequiredText("Department", EmployeeLimits.MaxTextLength);

        RuleFor(x => x.Email)
            .RequiredText("Email", EmployeeLimits.MaxEmailLength)
            .Must(email => EmailFormat().IsMatch(email!.Trim())).WithMessage("Email must be a valid email address.");

        RuleFor(x => x.HireDate)
            .NotNull().WithMessage("Hire date is required.")
            .Must(date => date <= DateOnly.FromDateTime(clock.UtcNow)).WithMessage("Hire date cannot be in the future.")
            .Must(date => date >= MinHireDate).WithMessage($"Hire date cannot be before {MinHireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.");

        RuleFor(x => x.BaseMonthlySalary).ValidMoney("Base monthly salary");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(status => Enum.GetNames<EmployeeStatus>().Contains(status)).WithMessage("Status must be Active or Inactive.");

        PayItems(x => x.Allowances, nameof(EmployeeInput.Allowances), "allowances");
        PayItems(x => x.Deductions, nameof(EmployeeInput.Deductions), "deductions");
    }

    private void PayItems(Expression<Func<T, IEnumerable<PayItemInput>?>> list, string propertyName, string label)
    {
        RuleFor(list)
            .Must(items => items is null || items.Count() <= MaxPayItems).WithMessage($"At most {MaxPayItems} {label} are allowed.")
            .Custom((items, context) => ReportDuplicateNames(items, propertyName, context));

        RuleForEach(list!)
            .NotNull().WithMessage("Item is required.")
            .SetValidator(new PayItemInputValidator());
    }

    private static void ReportDuplicateNames(IEnumerable<PayItemInput>? items, string propertyName, ValidationContext<T> context)
    {
        if (items is null)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, i) in items.Select((item, i) => (item, i)))
        {
            var name = item?.Name?.Trim();
            if (!string.IsNullOrEmpty(name) && !seen.Add(name))
                context.AddFailure($"{propertyName}[{i}].{nameof(PayItemInput.Name)}", "Name must be unique within the list.");
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex EmailFormat();
}

internal sealed class PayItemInputValidator : AbstractValidator<PayItemInput>
{
    public const int MaxNameLength = 50;

    public PayItemInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Name).RequiredText("Name", MaxNameLength);
        RuleFor(x => x.Amount).ValidMoney("Amount");
    }
}

internal static class InputRules
{
    public const decimal MaxAmount = 1_000_000m;

    /// <summary>Not blank, and at most <paramref name="maxLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, string label, int maxLength) =>
        rule.NotEmpty().WithMessage($"{label} is required.")
            .Must(value => value!.Trim().Length <= maxLength).WithMessage($"{label} must be at most {maxLength} characters.");

    /// <summary>Required, &gt; 0, ≤ 1,000,000, at most 2 decimals (salary and pay items).</summary>
    public static IRuleBuilderOptions<T, decimal?> ValidMoney<T>(this IRuleBuilder<T, decimal?> rule, string label) =>
        rule.NotNull().WithMessage($"{label} is required.")
            .GreaterThan(0m).WithMessage($"{label} must be greater than 0.")
            .LessThanOrEqualTo(MaxAmount).WithMessage(string.Create(CultureInfo.InvariantCulture, $"{label} must be at most {MaxAmount:N0}."))
            .Must(amount => decimal.Round(amount!.Value, 2) == amount).WithMessage($"{label} must have at most 2 decimal places.");
}
