namespace Employees.Domain;

/// <summary>Text limits shared by the input validator and the column lengths of the EF mapping.</summary>
public static class EmployeeLimits
{
    public const int MaxTextLength = 100;
    public const int MaxEmailLength = 254;
}
