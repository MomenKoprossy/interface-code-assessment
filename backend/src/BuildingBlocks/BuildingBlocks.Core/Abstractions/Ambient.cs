namespace BuildingBlocks.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);

    /// <summary>The value of the first claim of <paramref name="claimType"/>, or null.</summary>
    string? FindClaim(string claimType);
}

/// <summary>Use instead of DateTime.UtcNow so time is testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
