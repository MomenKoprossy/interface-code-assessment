using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Api.IntegrationTests.ApiFactory))]

namespace Api.IntegrationTests;

/// <summary>
/// One throwaway Postgres container and one API host for the whole run. Program.cs migrates and seeds at startup,
/// so the demo employees and logins from the seeders exist before the first test.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Integration-Test-Pa55!";
    public const string HrEmail = "hr@acme.test";
    public const string SaraEmail = "sara.ahmed@acme.test";
    public const string OmarEmail = "omar.hassan@acme.test";

    /// <summary>
    /// The API's clock is pinned to the run's start, so "today" cannot change mid-run (midnight UTC). It must be the
    /// real start time: JWTs take iat/exp from this clock while JwtBearer validates them against real time.
    /// </summary>
    public static readonly DateTime StartedUtc = DateTime.UtcNow;
    public static DateOnly Today => DateOnly.FromDateTime(StartedUtc);

    private const string JwtTestKey = "integration-tests-only-signing-key-0123456789abcdef0123456789ab"; // 64 chars

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly Dictionary<string, Task<LoginBody>> _logins = [];

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server; // build the host now: migrations + seeding run once, before any test
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:appdb", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", JwtTestKey);
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        // The platform limiter partitions by client IP and every TestServer request shares one partition.
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:LoginPermitLimit", "100000");
        builder.UseSetting("OpenApi:Enabled", "true");
        builder.ConfigureTestServices(services => services.AddSingleton<IClock, PinnedClock>());
    }

    private sealed class PinnedClock : IClock
    {
        public DateTime UtcNow => StartedUtc;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Logs in once per user (PBKDF2 is deliberately slow) and caches the response for the run.</summary>
    public Task<LoginBody> LoginAsync(string email)
    {
        lock (_logins)
        {
            if (!_logins.TryGetValue(email, out var login))
                _logins[email] = login = LoginUncachedAsync(email);
            return login;
        }
    }

    private async Task<LoginBody> LoginUncachedAsync(string email)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginBody>())!;
    }

    public async Task<HttpClient> ClientForAsync(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", (await LoginAsync(email)).AccessToken);
        return client;
    }

    public Task<HttpClient> HrClientAsync() => ClientForAsync(HrEmail);
    public Task<HttpClient> SaraClientAsync() => ClientForAsync(SaraEmail);
    public Task<HttpClient> OmarClientAsync() => ClientForAsync(OmarEmail);
}
