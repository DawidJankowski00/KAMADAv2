using FluentValidation;

namespace Kamada.Modules.Workforce.Application;

/// <summary>
/// Input rules shared by worker validators; also used by EF configuration for column lengths.
/// </summary>
internal static class WorkerRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int NameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;

    public static IRuleBuilderOptions<T, string> ValidUsername<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(UsernameMinLength, UsernameMaxLength)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Username may contain only letters, digits, '.', '_' and '-'.");

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(PasswordMinLength, PasswordMaxLength)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, string> PhoneNumber<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(PhoneNumberMaxLength)
            .Matches(@"^\+?[0-9 ]{9,19}$")
            .WithMessage("Phone number must contain 9-19 digits, optionally prefixed with '+'.");
}
