namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Client-side mirror of the server password policy (length only; the server also refuses common passwords and
/// validates again).
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 12;

    public const string Hint = "Min. 12 znaków. Najlepiej kilka słów, np. zdanie, którego nikt nie zgadnie.";

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return null;
        }

        return password.Length < MinLength ? $"Hasło musi mieć co najmniej {MinLength} znaków." : null;
    }
}
