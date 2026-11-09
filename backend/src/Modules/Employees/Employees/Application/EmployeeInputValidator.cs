using System.Globalization;
using System.Linq.Expressions;
using BuildingBlocks.Abstractions;
using Employees.Domain;
using FluentValidation;

namespace Employees.Application;

/// <summary>
/// The validation rules table of the API contract, shared by create and update. The rules themselves live in
/// <see cref="EmployeeRules"/> (the aggregate guards with the same ones); this class only adds the field-level
/// messages. Text lengths are checked after trimming, because the trimmed value is what gets stored.
/// </summary>
internal abstract class EmployeeInputValidator<T> : AbstractValidator<T>
    where T : EmployeeInput
{
    protected EmployeeInputValidator(IClock clock)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FullName).RequiredText("Full name", EmployeeRules.MaxTextLength);
        RuleFor(x => x.JobTitle).RequiredText("Job title", EmployeeRules.MaxTextLength);
        RuleFor(x => x.Department).RequiredText("Department", EmployeeRules.MaxTextLength);

        RuleFor(x => x.Email)
            .RequiredText("Email", EmployeeRules.MaxEmailLength)
            
            .Must(email => EmployeeRules.IsValidEmailFormat(email!)).WithMessage("Email must be a valid email address.");

        RuleFor(x => x.HireDate)
            .NotNull().WithMessage("Hire date is required.")
            .Must(date => date <= DateOnly.FromDateTime(clock.UtcNow)).WithMessage("Hire date cannot be in the future.")
            .Must(date => date >= EmployeeRules.MinHireDate).WithMessage($"Hire date cannot be before {EmployeeRules.MinHireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.");

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
            .Must(items => items is null || items.Count() <= EmployeeRules.MaxPayItems).WithMessage($"At most {EmployeeRules.MaxPayItems} {label} are allowed.")
            .Custom((items, context) => ReportDuplicateNames(items, propertyName, context));

        RuleForEach(list!)
            .NotNull().WithMessage("Item is required.")
            .SetValidator(new PayItemInputValidator());
    }

    private static void ReportDuplicateNames(IEnumerable<PayItemInput>? items, string propertyName, ValidationContext<T> context)
    {
        if (items is null)
            return;

        var seen = new HashSet<string>();
        foreach (var (item, i) in items.Select((item, i) => (item, i)))
        {
            if (!string.IsNullOrWhiteSpace(item?.Name) && !seen.Add(EmployeeRules.PayItemNameKey(item.Name)))
                context.AddFailure($"{propertyName}[{i}].{nameof(PayItemInput.Name)}", "Name must be unique within the list.");
        }
    }
}

internal sealed class PayItemInputValidator : AbstractValidator<PayItemInput>
{
    public PayItemInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
        RuleFor(x => x.Name).RequiredText("Name", EmployeeRules.MaxPayItemNameLength);
        RuleFor(x => x.Amount).ValidMoney("Amount");
    }
}

internal static class InputRules
{
    /// <summary>Not blank, and at most <paramref name="maxLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string?> RequiredText<T>(this IRuleBuilder<T, string?> rule, string label, int maxLength) =>
        rule.NotEmpty().WithMessage($"{label} is required.")
            .Must(value => value!.Trim().Length <= maxLength).WithMessage($"{label} must be at most {maxLength} characters.");

    /// <summary>Required, &gt; 0, ≤ 1,000,000, at most 2 decimals (salary and pay items).</summary>
    public static IRuleBuilderOptions<T, decimal?> ValidMoney<T>(this IRuleBuilder<T, decimal?> rule, string label) =>
        rule.NotNull().WithMessage($"{label} is required.")
            .GreaterThan(0m).WithMessage($"{label} must be greater than 0.")
            .LessThanOrEqualTo(EmployeeRules.MaxAmount).WithMessage(string.Create(CultureInfo.InvariantCulture, $"{label} must be at most {EmployeeRules.MaxAmount:N0}."))
            .Must(amount => EmployeeRules.HasAtMostTwoDecimals(amount!.Value)).WithMessage($"{label} must have at most 2 decimal places.");
}
