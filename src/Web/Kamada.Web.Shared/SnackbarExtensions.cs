using Kamada.Web.Shared.Api;
using MudBlazor;

namespace Kamada.Web.Shared;

public static class SnackbarExtensions
{
    /// <summary>Shows a success message, or the API problem as an error. Returns whether the call succeeded.</summary>
    public static bool Report(this ISnackbar snackbar, ApiResult result, string? successMessage = null)
    {
        if (result.IsSuccess)
        {
            if (successMessage is not null)
            {
                snackbar.Add(successMessage, Severity.Success);
            }

            return true;
        }

        snackbar.Add(result.Problem!.Message, result.Problem.Status is >= 500 or 0 ? Severity.Error : Severity.Warning);
        return false;
    }
}
