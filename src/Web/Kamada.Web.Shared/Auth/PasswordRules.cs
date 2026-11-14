namespace Kamada.Web.Shared.Auth;

/// <summary>Client-side mirror of the server password policy (the server validates again).</summary>
public static class PasswordRules
{
    public const string Hint = "Min. 8 znaków, mała i wielka litera, cyfra";

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return null;
        }

        if (password.Length < 8)
        {
            return "Hasło musi mieć co najmniej 8 znaków.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Hasło musi zawierać małą i wielką literę oraz cyfrę.";
        }

        return null;
    }
}
