using BuildingBlocks.Abstractions;
using Employees.Application;
using Employees.Domain;
using Microsoft.EntityFrameworkCore;

namespace Employees.Infrastructure.Persistence;

/// <summary>EF Core (PostgreSQL) adapter for <see cref="IEmployeeRepository"/>. The soft-delete query filter hides deleted rows.</summary>
internal sealed class EmployeeRepository(DbContext db) : IEmployeeRepository
{
    private DbSet<Employee> Employees => db.Set<Employee>();

    public Task<Employee?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Guid?> FindIdByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        await Employees
            .Where(e => e.Email == normalizedEmail)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedList<Employee>> SearchAsync(
        EmployeeSearchCriteria criteria, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = Employees.AsNoTracking();

        if (criteria.Search is { } search)
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.FullName, pattern, LikeEscape) || EF.Functions.ILike(e.Email, pattern, LikeEscape));
        }

        if (criteria.Department is { } department)
        {
            var lowered = department.ToLower();
            query = query.Where(e => e.Department.ToLower() == lowered);
        }

        if (criteria.Status is { } status)
            query = query.Where(e => e.Status == status);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.FullName)
            .ThenBy(e => e.Id)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        return new PagedList<Employee>(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<string>> ListDepartmentsAsync(CancellationToken cancellationToken = default) =>
        await Employees.Select(e => e.Department).Distinct().OrderBy(d => d).ToListAsync(cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
        await Employees.AddAsync(employee, cancellationToken);

    public void Remove(Employee employee) => Employees.Remove(employee);

    private const string LikeEscape = "\\";

    // The user's text is matched literally: LIKE wildcards in it must not act as wildcards.
    internal static string EscapeLikePattern(string text) =>
        text.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
}
