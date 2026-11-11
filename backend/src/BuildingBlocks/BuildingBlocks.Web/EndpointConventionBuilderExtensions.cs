using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Web;

public static class EndpointConventionBuilderExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(HasPermissionAttribute.PolicyPrefix + permission);
}
