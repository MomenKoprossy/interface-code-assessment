using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using EmployeePayroll.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public sealed class OpsTests(ApiFactory api)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Health_endpoints_are_healthy_without_a_token(string path)
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task OpenApi_document_declares_bearer_scheme_and_employee_paths()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().Should().Be("http");
        bearer.GetProperty("scheme").GetString().Should().Be("bearer");

        var paths = root.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.Should().Contain([
            "/api/employees",
            "/api/employees/{id}",
            "/api/employees/departments",
            "/api/employees/{id}/payslip",
            "/api/payroll/policy",
            "/api/auth/login",
            "/api/auth/me",
        ]);
    }

    [Fact]
    public async Task Swagger_ui_is_served()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    // Outside Development the docs exist only behind OpenApi:Enabled. Testing is not Development, so this isolates the flag.
    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task Api_docs_are_404_when_OpenApi_is_disabled(string path)
    {
        await using var noDocs = api.WithWebHostBuilder(b => b.UseSetting("OpenApi:Enabled", "false"));
        using var client = noDocs.CreateClient();

        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // `dotnet ef migrations add` builds the model from DesignTimeAppDbContextFactory's hard-coded module list; a module
    // missing there would silently drop its tables from the next migration.
    [Fact]
    public void Design_time_model_equals_the_runtime_model()
    {
        using var scope = api.Services.CreateScope();
        using var designTime = new DesignTimeAppDbContextFactory().CreateDbContext([]);

        Tables(designTime).Should().Equal(Tables(scope.ServiceProvider.GetRequiredService<AppDbContext>()));
    }

    private static List<string> Tables(DbContext db) =>
        db.Model.GetEntityTypes().Select(e => $"{e.GetSchema()}.{e.GetTableName()} <- {e.Name}").Order().ToList();
}
