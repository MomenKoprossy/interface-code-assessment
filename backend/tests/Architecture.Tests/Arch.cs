using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace Architecture.Tests;

/// <summary>The solution's assemblies, grouped the way docs/architecture.md §2 talks about them.</summary>
internal static class Arch
{
    public const string Api = "EmployeePayroll.Api";
    public static readonly string[] Modules = ["Employees", "Payroll", "Identity"];

    public static Assembly Load(string name) => Assembly.Load(name);

    public static Assembly Core(string module) => Load(module);
    public static Assembly Infrastructure(string module) => Load($"{module}.Infrastructure");
    public static Assembly EmployeesContracts => Load("Employees.Contracts");

    public static IEnumerable<Assembly> Cores => Modules.Select(Core);

    // Only the blocks in the app's dependency graph (ADR-002); Messaging/Grpc/Storage live in BuildingBlocks.slnx.
    public static readonly string[] BuildingBlocks = ["BuildingBlocks.Core", "BuildingBlocks.Data", "BuildingBlocks.Web"];

    public static IEnumerable<Assembly> All =>
        Modules.SelectMany(m => new[] { Core(m), Infrastructure(m) })
            .Append(EmployeesContracts)
            .Concat(BuildingBlocks.Select(Load))
            .Append(Load(Api));

    /// <summary>
    /// Full names of every top-level type in <paramref name="assemblies"/>. Used as NetArchTest dependency
    /// patterns when a namespace prefix would be too coarse (e.g. "Employees" would also match "Employees.Contracts").
    /// </summary>
    public static string[] TypeNamesOf(params Assembly[] assemblies) =>
        assemblies.SelectMany(a => a.GetTypes())
            .Where(t => !t.IsNested && !t.Name.StartsWith('<'))
            .Select(t => t.FullName!)
            .ToArray();

    public static TypeInfo[] TypesOf(Assembly assembly) =>
        assembly.DefinedTypes.Where(t => !t.Name.StartsWith('<') && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))).ToArray();

    public static void ShouldPass(this NetArchTest.Rules.TestResult result, string rule)
    {
        result.SelectedTypesForTesting.Should().NotBeEmpty($"rule '{rule}' must apply to some types, or it proves nothing");
        var failing = result.FailingTypes?.Select(t => t.FullName).ToArray() ?? [];
        failing.Should().BeEmpty($"{rule}. Offending types: {string.Join(", ", failing)}");
    }
}
