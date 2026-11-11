// Secrets are Aspire parameters: the JWT signing key is generated once and persisted in this project's user secrets;
// the demo-user password is read from user secrets (Parameters:demo-password) or prompted by the dashboard.

var builder = DistributedApplication.CreateBuilder(args);

var jwtKey = builder.AddParameter("jwt-key", new GenerateParameterDefault { MinLength = 64, Special = false }, secret: true, persist: true);
var demoPassword = builder.AddParameter("demo-password", secret: true);

var database = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("appdb");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Seed__Enabled", "true")
    .WithEnvironment("Seed__DemoPassword", demoPassword)
    .WithHttpHealthCheck("/health");

builder.AddViteApp("web", "../../../frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
