using Kamada.Mobile.Platform;

namespace Kamada.Mobile;

public partial class App : Application
{
    private readonly ConnectivityNetworkEvents _networkEvents;

    public App(ConnectivityNetworkEvents networkEvents)
    {
        _networkEvents = networkEvents;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "KAMADA" };
        window.Resumed += async (_, _) => await _networkEvents.OnResumedAsync();
        return window;
    }
}
