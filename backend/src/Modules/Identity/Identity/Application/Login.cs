using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using FluentValidation;
using Identity.Domain;

namespace Identity.Application;

public sealed record LoginCommand(string Email, string Password) : ICommand<Result<LoginResponse>>;

/// <summary>The contract's <c>LoginResponse</c>.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public const int EmailMaxLength = 254;
    public const int PasswordMaxLength = 128;

    public LoginCommandValidator()
    {
        RuleFor(c => c.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(EmailMaxLength).WithMessage($"Email must be at most {EmailMaxLength} characters.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Email must be a valid email address.");

        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(PasswordMaxLength).WithMessage($"Password must be at most {PasswordMaxLength} characters.");
    }
}

internal sealed class LoginCommandHandler(IUserRepository users, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    // One message for unknown email and wrong password, so the response doesn't reveal which accounts exist.
    public static readonly Error InvalidCredentials = Error.Unauthorized("Invalid email or password.");

    public async ValueTask<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);

        // Always verify (null hash → dummy verify) so unknown emails take as long as wrong passwords.
        var passwordMatches = passwordHasher.Verify(user?.PasswordHash, request.Password);
        if (user is null || !passwordMatches)
            return InvalidCredentials;

        var token = tokenIssuer.Issue(user);
        return new LoginResponse(token.Value, token.ExpiresAtUtc, CurrentUserDto.From(user));
    }
}
