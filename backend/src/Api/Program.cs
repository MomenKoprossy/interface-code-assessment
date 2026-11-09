using System.Text.Json.Serialization;
using BuildingBlocks.Data;
using BuildingBlocks.Web;
using Employees;
using Employees.Infrastructure;
using EmployeePayroll.Api.Persistence;
using EmployeePayroll.Api.OpenApi;
using Identity;
using Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Payroll;
using Payroll.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                 // OpenTelemetry logs/traces/metrics, /health + /alive, resilience
builder.AddPlatformObservability();           // EF Core SQL spans + metrics
builder.AddPlatform(platform =>               // ProblemDetails, mediator + validators, JWT + permissions, rate limit, headers
{
    platform.HandlerAssemblies.Add(EmployeesAssembly.Reference);
    platform.HandlerAssemblies.Add(PayrollAssembly.Reference);
    platform.HandlerAssemblies.Add(IdentityAssembly.Reference);
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString(AppDbContext.ConnectionStringName),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", AppDbContext.PlatformSchema)));
builder.Services.AddPlatformRepositories<AppDbContext>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

builder.AddEmployeesModule();
builder.AddPayrollModule();
builder.AddIdentityModule();

builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(openApi => openApi.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

app.UsePlatform();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.UseSwaggerUI(swagger =>
{
    swagger.SwaggerEndpoint("/openapi/v1.json", "Employee Payroll API v1");
    swagger.RoutePrefix = "swagger";
});

var api = app.MapGroup("/api");
api.MapIdentityEndpoints();
api.MapEmployeesEndpoints();
api.MapPayrollEndpoints();

await app.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;                 // WebApplicationFactory<Program> in integration tests
