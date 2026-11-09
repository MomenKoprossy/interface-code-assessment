using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace Api.IntegrationTests;

// Response/request shapes from docs/api-contract.md. Enums are plain strings, as on the wire.
public sealed record CurrentUserBody(Guid Id, string Email, string DisplayName, string Role, Guid? EmployeeId, string[] Permissions);
public sealed record LoginBody(string AccessToken, DateTime ExpiresAtUtc, CurrentUserBody User);

public sealed record PayItemBody(string Name, decimal Amount);

public sealed record EmployeeInputBody(
    string? FullName, string? Email, string? JobTitle, string? Department, DateOnly? HireDate,
    decimal? BaseMonthlySalary, string? Status, PayItemBody[]? Allowances, PayItemBody[]? Deductions);

public sealed record EmployeeBody(
    Guid Id, string FullName, string Email, string JobTitle, string Department, DateOnly HireDate,
    decimal BaseMonthlySalary, string Status, PayItemBody[]? Allowances, PayItemBody[]? Deductions);

public sealed record PageBody(
    EmployeeBody[] Items, int Page, int PageSize, long TotalCount, int TotalPages, bool HasPrevious, bool HasNext);

public sealed record SocialInsuranceBody(decimal Rate, decimal? MonthlyCap, decimal UncappedAmount, decimal Amount, bool CapApplied);
public sealed record TaxBracketLineBody(decimal From, decimal? UpTo, decimal Rate, decimal TaxedAmount, decimal Tax);
public sealed record IncomeTaxBody(decimal Amount, TaxBracketLineBody[] Brackets);

public sealed record PayslipBody(
    Guid EmployeeId, string EmployeeName, string Department, string JobTitle, string Month, string Currency,
    decimal BaseSalary, PayItemBody[] Allowances, decimal TotalAllowances, decimal GrossPay,
    SocialInsuranceBody SocialInsurance, decimal TaxableIncome, IncomeTaxBody IncomeTax,
    PayItemBody[] OtherDeductions, decimal TotalOtherDeductions, decimal TotalDeductions, decimal NetPay, string RoundingRule);

public sealed record ProblemBody(
    string Type, string Title, int Status, string? Detail, string TraceId, IReadOnlyDictionary<string, string[]> Errors);

public static class EmployeeFactory
{
    public static string UniqueEmail(string prefix = "it") => $"{prefix}-{Guid.NewGuid():N}@acme.test";

    /// <summary>A unique token safe for names, departments and LIKE tests (letters and digits only).</summary>
    public static string Token() => "T" + Guid.NewGuid().ToString("N")[..12];

    /// <summary>A valid <c>EmployeeInput</c> with a unique email; tweak it with <c>with { … }</c>.</summary>
    public static EmployeeInputBody ValidInput(
        string? fullName = null, string? department = null, decimal salary = 5000m, string status = "Active",
        DateOnly? hireDate = null, PayItemBody[]? allowances = null, PayItemBody[]? deductions = null) =>
        new(fullName ?? $"Test Employee {Token()}",
            UniqueEmail(),
            "Software Engineer",
            department ?? "Engineering",
            hireDate ?? new DateOnly(2020, 1, 15),
            salary,
            status,
            allowances ?? [new("Transport", 500m)],
            deductions ?? []);

    public static async Task<EmployeeBody> CreateAsync(HttpClient hr, EmployeeInputBody input)
    {
        var response = await hr.PostAsJsonAsync("/api/employees", input);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<EmployeeBody>())!;
    }
}

public static class HttpAssertions
{
    /// <summary>
    /// Asserts the contract's error shape: <c>application/problem+json</c> with type, title, status (matching the
    /// HTTP status) and a traceId. Returns the parsed body; <see cref="ProblemBody.Errors"/> is empty when absent.
    /// </summary>
    public static async Task<ProblemBody> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(status, raw);
        response.Content.Headers.ContentType.Should().NotBeNull(raw);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json", raw);

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;
        var errors = root.TryGetProperty("errors", out var e)
            ? e.Deserialize<Dictionary<string, string[]>>()!
            : new Dictionary<string, string[]>();

        var problem = new ProblemBody(
            root.GetProperty("type").GetString()!,
            root.GetProperty("title").GetString()!,
            root.GetProperty("status").GetInt32(),
            root.TryGetProperty("detail", out var d) ? d.GetString() : null,
            root.TryGetProperty("traceId", out var t) ? t.GetString()! : string.Empty,
            errors);

        problem.Status.Should().Be((int)status);
        problem.Type.Should().NotBeNullOrWhiteSpace();
        problem.Title.Should().NotBeNullOrWhiteSpace();
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
        return problem;
    }

    public static async Task<T> ShouldBeOkJsonAsync<T>(this HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        response.StatusCode.Should().Be(status, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
