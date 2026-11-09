namespace BuildingBlocks.Abstractions;

/// <summary>Ambient identity for the current request — injected, never static (see foundation doc).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? Id { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool HasPermission(string permission);

    /// <summary>The value of the first claim of <paramref name="claimType"/>, or null (resource-ownership rules).</summary>
    string? FindClaim(string claimType);
}

/// <summary>Injectable clock so time is testable — never call DateTime.UtcNow directly in logic.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
