using Kamada.Web.Shared.Components;
using MudBlazor;

namespace Kamada.Web.Shared;

public static class DialogServiceExtensions
{
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogs,
        string title,
        string message,
        string confirmText = "Potwierdź",
        Color confirmColor = Color.Primary)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { d => d.Message, message },
            { d => d.ConfirmText, confirmText },
            { d => d.ConfirmColor, confirmColor },
        };
        var dialog = await dialogs.ShowAsync<ConfirmDialog>(title, parameters, new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true });
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    /// <summary>Shows a dialog and returns its result value, or default when cancelled.</summary>
    public static async Task<TResult?> ShowForResultAsync<TDialog, TResult>(
        this IDialogService dialogs,
        string title,
        DialogParameters<TDialog>? parameters = null,
        MaxWidth maxWidth = MaxWidth.Small)
        where TDialog : Microsoft.AspNetCore.Components.IComponent
    {
        var dialog = await dialogs.ShowAsync<TDialog>(
            title,
            parameters ?? [],
            new DialogOptions { MaxWidth = maxWidth, FullWidth = true, CloseOnEscapeKey = true });
        var result = await dialog.Result;
        return result is { Canceled: false, Data: TResult value } ? value : default;
    }
}
