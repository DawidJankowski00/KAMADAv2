// Links that open a new tab in the browser (target="_blank": Google Maps navigation, ...) do nothing in Android's
// WebView, which has no tabs. Turn them into a plain navigation: BlazorWebView opens every address outside the app
// with the system (UrlLoadingStrategy.OpenExternally), so a Maps link starts the Google Maps app.
document.addEventListener("click", (event) => {
  const link = event.target instanceof Element ? event.target.closest("a[target=_blank][href]") : null;
  if (!link || link.origin === window.location.origin) return;
  event.preventDefault();
  window.location.href = link.href;
}, true);
