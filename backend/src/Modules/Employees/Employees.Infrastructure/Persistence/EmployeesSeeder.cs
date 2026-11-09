using BuildingBlocks.Abstractions;
using BuildingBlocks.Data;
using Employees.Domain;
using Microsoft.EntityFrameworkCore;

namespace Employees.Infrastructure.Persistence;

/// <summary>
/// Demo data. Runs once: skipped as soon as any employee row exists (deleted ones included).
/// Sara Ahmed and Omar Hassan are the employees the Identity module links its demo logins to, by email.
/// </summary>
internal sealed class EmployeesSeeder(DbContext db, IClock clock) : IDataSeeder
{
    public int Order => 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Set<Employee>().IgnoreQueryFilters().AnyAsync(cancellationToken))
            return;

        var today = DateOnly.FromDateTime(clock.UtcNow);
        db.Set<Employee>().AddRange(DemoEmployees().Select(data => Employee.Create(data, today)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<EmployeeData> DemoEmployees()
    {
        const EmployeeStatus active = EmployeeStatus.Active;
        const EmployeeStatus inactive = EmployeeStatus.Inactive;
        PayItem transport = new("Transport", 500m), housing = new("Housing", 1500m), meal = new("Meal", 300m);
        PayItem loan = new("Loan repayment", 400m), union = new("Union dues", 50m);

        yield return new("Sara Ahmed", "sara.ahmed@acme.test", "Senior Software Engineer", "Engineering", new(2019, 3, 4), 8500m, active, [transport, housing], []);
        yield return new("Omar Hassan", "omar.hassan@acme.test", "Financial Analyst", "Finance", new(2021, 6, 14), 6200m, active, [transport, meal], [union]);
        yield return new("Layla Mansour", "layla.mansour@acme.test", "Engineering Manager", "Engineering", new(2016, 9, 1), 12500m, active, [transport, housing, meal], [loan]);
        yield return new("Karim Youssef", "karim.youssef@acme.test", "Software Engineer", "Engineering", new(2023, 1, 9), 5400m, active, [], []);
        yield return new("Nadia Farouk", "nadia.farouk@acme.test", "QA Engineer", "Engineering", new(2020, 11, 16), 4800m, active, [meal], [union]);
        yield return new("Youssef Ibrahim", "youssef.ibrahim@acme.test", "DevOps Engineer", "Engineering", new(2018, 5, 21), 7300m, inactive, [transport], []);
        yield return new("Mona Khalil", "mona.khalil@acme.test", "Finance Manager", "Finance", new(2017, 2, 13), 9800m, active, [transport, housing], [loan]);
        yield return new("Tarek Saleh", "tarek.saleh@acme.test", "Accountant", "Finance", new(2024, 4, 1), 4200m, active, [], []);
        yield return new("Huda Nasser", "huda.nasser@acme.test", "HR Manager", "Human Resources", new(2016, 7, 18), 8800m, active, [transport, housing, meal], []);
        yield return new("Rami Aziz", "rami.aziz@acme.test", "Recruiter", "Human Resources", new(2022, 8, 29), 3900m, active, [meal], [union]);
        yield return new("Dina Shaker", "dina.shaker@acme.test", "Sales Director", "Sales", new(2018, 10, 8), 9500m, active, [transport, housing], []);
        yield return new("Ahmed Fathy", "ahmed.fathy@acme.test", "Account Executive", "Sales", new(2025, 2, 3), 4500m, active, [transport], [loan]);
        yield return new("Salma Adel", "salma.adel@acme.test", "Sales Representative", "Sales", new(2019, 12, 2), 3600m, inactive, [], [union]);
        yield return new("Hassan Zaki", "hassan.zaki@acme.test", "Operations Manager", "Operations", new(2017, 6, 26), 8200m, active, [transport, meal], []);
        yield return new("Mariam Lotfy", "mariam.lotfy@acme.test", "Logistics Coordinator", "Operations", new(2021, 3, 15), 4100m, active, [], []);
    }
}
