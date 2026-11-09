using Identity.Domain;

namespace Identity.Application;

/// <summary>The contract's <c>CurrentUser</c>.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    Role Role,
    Guid? EmployeeId,
    IReadOnlyList<string> Permissions)
{
    public static CurrentUserDto From(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.EmployeeId, user.Permissions);
}
