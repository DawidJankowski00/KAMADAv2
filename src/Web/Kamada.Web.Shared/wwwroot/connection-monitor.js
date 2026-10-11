// Browser online/offline events for ConnectionMonitor (BrowserNetworkEvents). navigator.onLine only knows whether the
// device has a network, so the .NET side confirms "online" with a ping of the API.

let listeners = null;

/** Starts forwarding the events; returns the current navigator.onLine. */
export function start(dotnet) {
  stop();
  const online = () => dotnet.invokeMethodAsync("OnBrowserOnlineChanged", true);
  const offline = () => dotnet.invokeMethodAsync("OnBrowserOnlineChanged", false);
  // Phones suspend background tabs; check again when the app is shown.
  const visible = () => {
    if (document.visibilityState === "visible") dotnet.invokeMethodAsync("OnBrowserOnlineChanged", navigator.onLine);
  };
  window.addEventListener("online", online);
  window.addEventListener("offline", offline);
  document.addEventListener("visibilitychange", visible);
  listeners = { online, offline, visible };
  return navigator.onLine;
}

export function stop() {
  if (!listeners) return;
  window.removeEventListener("online", listeners.online);
  window.removeEventListener("offline", listeners.offline);
  document.removeEventListener("visibilitychange", listeners.visible);
  listeners = null;
}
