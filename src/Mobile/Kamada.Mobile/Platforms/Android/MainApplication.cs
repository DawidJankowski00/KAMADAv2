using Android.App;
using Android.Runtime;

namespace Kamada.Mobile;

// Plain HTTP only in Debug builds (local API on the emulator or in the LAN); Release talks to the API over HTTPS.
#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application(UsesCleartextTraffic = false)]
#endif
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
