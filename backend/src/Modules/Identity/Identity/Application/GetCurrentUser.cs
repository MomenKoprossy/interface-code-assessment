using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;

namespace Identity.Application;

public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserDto>>;

internal sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserRepository users)
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    public static readonly Error NotSignedIn = Error.Unauthorized("You are not signed in.");

    public async ValueTask<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return NotSignedIn;

        // A valid token whose user no longer exists is treated as signed out.
        var user = await users.FindAsync(userId, cancellationToken);
        return user is null ? NotSignedIn : CurrentUserDto.From(user);
    }
}
