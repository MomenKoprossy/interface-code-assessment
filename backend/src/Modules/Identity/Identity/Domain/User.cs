using BuildingBlocks.Abstractions;
using BuildingBlocks.Domain;

namespace Identity.Domain;

public enum Role { HR, Employee }

/// <summary>A login. HR users are not linked to an employee; Employee users always are.</summary>
public sealed class User : AggregateRoot<Guid>, IAuditable
{
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Role Role { get; private set; }
    public Guid? EmployeeId { get; private set; }

    public DateTime? CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public Guid? ModifiedBy { get; set; }

    private User() { } // EF

    private User(string email, string displayName, string passwordHash, Role role, Guid? employeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        DisplayName = displayName.Trim();
        PasswordHash = passwordHash;
        Role = role;
        EmployeeId = employeeId;
    }

    public static User CreateHr(string email, string displayName, string passwordHash) =>
        new(email, displayName, passwordHash, Role.HR, employeeId: null);

    public static User CreateEmployee(string email, string displayName, string passwordHash, Guid employeeId)
    {
        if (employeeId == Guid.Empty)
            throw new ArgumentException("An Employee login must be linked to an employee.", nameof(employeeId));

        return new(email, displayName, passwordHash, Role.Employee, employeeId);
    }

    public IReadOnlyList<string> Permissions => RolePermissions.For(Role);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
