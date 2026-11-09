namespace Employees.Domain;

/// <summary>A named fixed monthly amount: an allowance or an "other" deduction.</summary>
public sealed record PayItem(string Name, decimal Amount);
