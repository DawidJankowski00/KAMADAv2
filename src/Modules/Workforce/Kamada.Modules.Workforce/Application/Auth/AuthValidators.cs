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

internal sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(r => r.Email).WorkerEmail();
}

internal sealed class CompletePasswordResetRequestValidator : AbstractValidator<CompletePasswordResetRequest>
{
    public CompletePasswordResetRequestValidator()
    {
        RuleFor(r => r.Token).NotEmpty().MaximumLength(200);
        RuleFor(r => r.NewPassword).StrongPassword();
    }
}

internal sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(r => r.ChallengeToken).NotEmpty().MaximumLength(2000);
        RuleFor(r => r.Code).NotEmpty().MaximumLength(20);
    }
}

internal sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator() => RuleFor(r => r.Code).NotEmpty().MaximumLength(20);
}

internal sealed class DisableTwoFactorRequestValidator : AbstractValidator<DisableTwoFactorRequest>
{
    public DisableTwoFactorRequestValidator()
    {
        RuleFor(r => r.Password).NotEmpty().MaximumLength(WorkerRules.PasswordMaxLength);
        RuleFor(r => r.Code).NotEmpty().MaximumLength(20);
    }
}
