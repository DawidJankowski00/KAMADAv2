using FluentValidation;
using Kamada.Shared.Contracts.Auth;

namespace Kamada.Modules.Workforce.Application.Auth;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Username).NotEmpty().MaximumLength(WorkerRules.UsernameMaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(WorkerRules.PasswordMaxLength);
    }
}

internal sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(r => r.RefreshToken).NotEmpty().MaximumLength(200);
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().MaximumLength(WorkerRules.PasswordMaxLength);
        RuleFor(r => r.NewPassword)
            .StrongPassword()
            .NotEqual(r => r.CurrentPassword).WithMessage("The new password must differ from the current one.");
    }
}
