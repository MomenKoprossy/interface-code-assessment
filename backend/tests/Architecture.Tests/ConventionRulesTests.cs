using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using FluentValidation;

namespace Architecture.Tests;

/// <summary>Placement and coding conventions that NetArchTest's dependency rules can't express.</summary>
public sealed class ConventionRulesTests
{
    private static bool IsRequestHandler(Type t) =>
        !t.IsAbstract && !t.IsInterface &&
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

    private static bool IsValidator(Type t) => !t.IsInterface && typeof(IValidator).IsAssignableFrom(t);

    private static bool IsDomainEntity(Type t)
    {
        for (var b = t.BaseType; b is not null; b = b.BaseType)
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(Entity<>)) return true;
        return false;
    }

    [Fact]
    public void Api_is_a_composition_root_only()
    {
        var offenders = Arch.TypesOf(Arch.Load(Arch.Api))
            .Where(t => IsRequestHandler(t) || IsValidator(t) || IsDomainEntity(t))
            .Select(t => t.FullName)
            .ToArray();

        offenders.Should().BeEmpty(
            $"the Api only wires modules: handlers, validators and entities belong in module cores. Offending types: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Every_request_handler_lives_in_a_module_core_application_namespace()
    {
        var cores = Arch.Cores.ToHashSet();
        var handlers = Arch.All.SelectMany(Arch.TypesOf).Where(IsRequestHandler).ToArray();

        handlers.Should().NotBeEmpty("the modules define handlers, so an empty scan means the rule is looking in the wrong place");

        var offenders = handlers
            .Where(t => !cores.Contains(t.Assembly) || t.Namespace?.Contains(".Application") != true)
            .Select(t => $"{t.FullName} ({t.Assembly.GetName().Name})")
            .ToArray();

        offenders.Should().BeEmpty(
            $"IRequestHandler implementations belong in <Module>.Application inside the module core. Offending types: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("Employees")]
    [InlineData("Payroll")]
    [InlineData("Identity")]
    public void Module_core_exposes_no_floating_point_members(string module)
    {
        Type[] floating = [typeof(double), typeof(float), typeof(double?), typeof(float?)];
        const BindingFlags publicMembers = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var types = Arch.TypesOf(Arch.Core(module));
        types.Should().NotBeEmpty();

        var offenders = types
            .SelectMany(t =>
                t.GetProperties(publicMembers).Where(p => floating.Contains(p.PropertyType)).Select(p => $"{t.FullName}.{p.Name}")
                    .Concat(t.GetFields(publicMembers).Where(f => floating.Contains(f.FieldType)).Select(f => $"{t.FullName}.{f.Name}")))
            .ToArray();

        offenders.Should().BeEmpty($"money is decimal (ADR-007), never double/float. Offending members: {string.Join(", ", offenders)}");
    }

    [Theory]
    [InlineData("Employees")]
    [InlineData("Payroll")]
    [InlineData("Identity")]
    public void Module_core_source_never_reads_the_system_clock(string module)
    {
        // NetArchTest sees type dependencies, not member access, so DateTime.UtcNow is caught at source level.
        string[] banned = ["DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow"];
        var projectDir = Path.Combine(BackendRoot(), "src", "Modules", module, module);

        var files = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"))
            .ToArray();
        files.Should().NotBeEmpty($"{projectDir} should contain the {module} core sources");

        var offenders = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, line, i)))
            .Where(x => banned.Any(b => Regex.IsMatch(x.line, $@"\b{Regex.Escape(b)}\b")))
            .Select(x => $"{Path.GetRelativePath(projectDir, x.f)}:{x.i + 1}")
            .ToArray();

        offenders.Should().BeEmpty($"module cores take time from IClock so tests can pin it. Offending lines: {string.Join(", ", offenders)}");
    }

    private static string BackendRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "EmployeePayroll.slnx")))
                return dir.FullName;
        throw new InvalidOperationException($"EmployeePayroll.slnx not found above {AppContext.BaseDirectory}");
    }
}
