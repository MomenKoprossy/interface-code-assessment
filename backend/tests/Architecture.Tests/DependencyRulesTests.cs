using NetArchTest.Rules;

namespace Architecture.Tests;

/// <summary>Layer and module boundaries from docs/architecture.md §2 and ADR-003.</summary>
public sealed class DependencyRulesTests
{
    public static TheoryData<string> Modules => new(Arch.Modules);

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_core_is_free_of_frameworks_infrastructure_and_the_api(string module)
    {
        var result = Types.InAssembly(Arch.Core(module))
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql",
                "System.Net.Http",
                "Employees.Infrastructure",
                "Payroll.Infrastructure",
                "Identity.Infrastructure",
                Arch.Api)
            .GetResult();

        result.ShouldPass($"{module} core must not depend on EF Core, ASP.NET Core, Npgsql, HttpClient, any *.Infrastructure or the Api");
    }

    [Theory]
    [InlineData("Payroll")]
    [InlineData("Identity")]
    public void Module_core_uses_Employees_only_through_its_contracts(string module)
    {
        var employeesInternals = Arch.TypeNamesOf(Arch.Core("Employees"), Arch.Infrastructure("Employees"));

        var result = Types.InAssembly(Arch.Core(module))
            .ShouldNot()
            .HaveDependencyOnAny(employeesInternals)
            .GetResult();

        result.ShouldPass($"{module} core may reach Employees only through Employees.Contracts");
    }

    [Theory]
    [InlineData("Payroll", "Identity")]
    [InlineData("Identity", "Payroll")]
    public void Payroll_and_Identity_do_not_know_each_other(string module, string other)
    {
        var result = Types.InAssemblies([Arch.Core(module), Arch.Infrastructure(module)])
            .ShouldNot()
            .HaveDependencyOnAny(Arch.TypeNamesOf(Arch.Core(other), Arch.Infrastructure(other)))
            .GetResult();

        result.ShouldPass($"{module} must not depend on {other}");
    }

    [Fact]
    public void Employees_contracts_depend_only_on_the_BCL_and_BuildingBlocks_Core()
    {
        string[] allowed = ["System", "Employees.Contracts", .. Arch.TypeNamesOf(Arch.Load("BuildingBlocks.Core"))];

        var result = Types.InAssembly(Arch.EmployeesContracts)
            .ShouldNot()
            .HaveDependencyOtherThan(allowed)
            .GetResult();

        result.ShouldPass("Employees.Contracts is a public API: BCL (and BuildingBlocks.Core) only, no EF, ASP.NET or other blocks");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Infrastructure_does_not_reach_into_other_modules(string module)
    {
        var forbidden = Arch.Modules.Where(m => m != module)
            .SelectMany(m => Arch.TypeNamesOf(Arch.Core(m), Arch.Infrastructure(m)))
            .ToArray();

        var result = Types.InAssembly(Arch.Infrastructure(module))
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.ShouldPass($"{module}.Infrastructure may use other modules only through their *.Contracts");
    }

    [Theory]
    [InlineData("BuildingBlocks.Core")]
    [InlineData("BuildingBlocks.Data")]
    [InlineData("BuildingBlocks.Web")]
    public void BuildingBlocks_never_depend_on_modules_or_the_api(string block)
    {
        var moduleAndApiTypes = Arch.TypeNamesOf(Arch.All.Where(a => !a.GetName().Name!.StartsWith("BuildingBlocks.")).ToArray());

        var result = Types.InAssembly(Arch.Load(block))
            .ShouldNot()
            .HaveDependencyOnAny(moduleAndApiTypes)
            .GetResult();

        result.ShouldPass($"{block} is platform code and must not know any module or the Api");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_does_not_depend_on_application(string module)
    {
        var result = Types.InAssembly(Arch.Core(module))
            .That().ResideInNamespaceMatching($@"^{module}\.Domain(\.|$)")
            .ShouldNot()
            .HaveDependencyOnAny($"{module}.Application", "BuildingBlocks.Application")
            .GetResult();

        result.ShouldPass($"{module}.Domain must not depend on the application layer (dependencies point inward)");
    }
}
