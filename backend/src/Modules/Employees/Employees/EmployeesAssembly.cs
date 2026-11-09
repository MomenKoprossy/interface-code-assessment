using System.Reflection;

namespace Employees;

/// <summary>Assembly handle used by the host to register this module's handlers and validators.</summary>
public static class EmployeesAssembly
{
    public static readonly Assembly Reference = typeof(EmployeesAssembly).Assembly;
}
