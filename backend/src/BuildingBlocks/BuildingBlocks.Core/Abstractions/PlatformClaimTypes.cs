namespace BuildingBlocks.Abstractions;

/// <summary>Claim types the platform's authorization reads; token issuers must use the same names.</summary>
public static class PlatformClaimTypes
{
    /// <summary>One claim per granted permission (checked by <c>RequirePermission</c>).</summary>
    public const string Permission = "permission";
}
