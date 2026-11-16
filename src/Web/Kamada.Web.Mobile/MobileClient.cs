using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared;

namespace Kamada.Web.Mobile;

/// <summary>Client settings of the phone app, the same in every host.</summary>
public static class MobileClient
{
    public static KamadaClientOptions Options(Uri apiBaseUrl) => new()
    {
        ApiBaseUrl = apiBaseUrl,
        // Order = default role for a worker with several roles who has not picked one yet (most common daily work first).
        AllowedRoles = [Roles.Seller, Roles.Courier, Roles.Admin],
        StorageKey = "kamada.mobile.session",
        PreviousStorageKey = "kamada.courier.session",
        SnackbarPosition = MudBlazor.Defaults.Classes.Position.TopCenter,
    };
}
