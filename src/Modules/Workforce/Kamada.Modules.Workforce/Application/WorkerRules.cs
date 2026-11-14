using FluentValidation;
using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Shared.Contracts.Workforce;

namespace Kamada.Modules.Workforce.Application;

/// <summary>
/// Input rules shared by worker validators; also used by EF configuration for column lengths.
/// </summary>
internal static class WorkerRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int PasswordMaxLength = PasswordPolicy.MaxLength;
    public const int NameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int EmailMaxLength = 254;

    public static IRuleBuilderOptions<T, string> ValidUsername<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(UsernameMinLength, UsernameMaxLength)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may contain only letters, digits, '.', '_' and '-'.");

    /// <summary>A password being set (not one typed to sign in): see <see cref="PasswordPolicy"/>.</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule, PasswordPolicy policy) =>
        rule.NotEmpty()
            .Length(PasswordPolicy.MinLength, PasswordPolicy.MaxLength)
            .Must(password => !policy.IsCommon(password))
            .WithErrorCode("Password.TooCommon")
            .WithMessage("This password is one of the most common ones. Choose another.");

    public static IRuleBuilderOptions<T, string> WorkerEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(EmailMaxLength);

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, IReadOnlyList<WorkerRole>> WorkerRoles<T>(this IRuleBuilderInitial<T, IReadOnlyList<WorkerRole>> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Select at least one role.")
            .Must(roles => roles.All(role => Enum.IsDefined(role))).WithMessage("Unknown role.")
            .Must(roles => roles.Distinct().Count() == roles.Count).WithMessage("Each role may be given only once.");

    public static IRuleBuilderOptions<T, string> PhoneNumber<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(PhoneNumberMaxLength)
            .Matches(@"^\+?[0-9 ]{9,19}$")
            .WithMessage("Phone number must contain 9-19 digits, optionally prefixed with '+'.");
}
