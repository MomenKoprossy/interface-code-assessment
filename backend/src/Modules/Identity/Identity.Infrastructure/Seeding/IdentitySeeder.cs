using BuildingBlocks.Abstractions;
using BuildingBlocks.Data;
using Employees.Contracts;
using Identity.Application;
using Identity.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Demo logins: one HR user and two Employee users linked to seeded employees. Idempotent (existing emails are
/// skipped). Runs after the employees seeder. The password comes only from <c>Seed:DemoPassword</c>.
/// </summary>
internal sealed class IdentitySeeder(
    IConfiguration configuration,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IEmployeeDirectory employees,
    IUnitOfWork unitOfWork,
    ILogger<IdentitySeeder> logger) : IDataSeeder
{
    public const string DemoPasswordKey = "Seed:DemoPassword";

    private const string HrEmail = "hr@acme.test";
    private static readonly (string Email, string DisplayName)[] EmployeeUsers =
        [("sara.ahmed@acme.test", "Sara Ahmed"), ("omar.hassan@acme.test", "Omar Hassan")];

    public int Order => 20;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var password = configuration[DemoPasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping demo logins: {Key} is not set (env var Seed__DemoPassword).", DemoPasswordKey);
            return;
        }

        var hash = passwordHasher.Hash(password);

        if (!await users.AnyAsync(HrEmail, cancellationToken))
            await users.AddAsync(User.CreateHr(HrEmail, "Hana Rashed", hash), cancellationToken);

        foreach (var (email, displayName) in EmployeeUsers)
        {
            if (await users.AnyAsync(email, cancellationToken))
                continue;

            var employeeId = await employees.FindIdByEmailAsync(email, cancellationToken);
            if (employeeId is null)
            {
                logger.LogWarning("Skipping demo login {Email}: no employee with that email.", email);
                continue;
            }

            await users.AddAsync(User.CreateEmployee(email, displayName, hash, employeeId.Value), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
