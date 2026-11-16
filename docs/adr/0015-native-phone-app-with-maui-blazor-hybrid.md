# 0015. Native phone app with .NET MAUI Blazor Hybrid

- **Status:** Accepted
- **Date:** 2026-11-16
- **Supersedes:** the phone PWA of [0005](0005-blazor-wasm-frontends.md); amends the device storage of [0009](0009-offline-ordering-on-the-phone.md)

## Context

The phone app was a Blazor WebAssembly PWA. It worked, but a browser app on a worker's phone has limits that matter
here: the refresh token sits in `localStorage` (readable by any script that slips past the CSP – ASVS gap G11), the
offline copy of clients lives in IndexedDB that the browser may evict, the microphone needs HTTPS and a browser
permission prompt, and Google Maps links open a browser tab instead of the Maps app. Workers install it from a link,
not from an app store, so updates and versions are hard to track.

## Decision

- **.NET MAUI Blazor Hybrid, Android first.** `src/Mobile/Kamada.Mobile` hosts the same Razor pages in a
  `BlazorWebView`. The pages, layout and role areas moved to the Razor class library `Kamada.Web.Mobile`, so the UI
  is not rewritten; C# runs natively in the app process, the WebView only draws HTML.
- **Platform services behind interfaces in `Kamada.Web.Shared/Platform`.** In Blazor Hybrid a JS runtime exists only
  inside the WebView's scope, so the browser implementations (singletons calling `localStorage`, IndexedDB, the
  AudioWorklet) cannot work there. The native app registers its own before `AddKamadaClient`, which adds the browser
  ones only when none is registered:
  - `ISessionStorage` → MAUI `SecureStorage` (Android Keystore): the token pair and the remembered role;
  - `ILocalStore` → SQLite (`Microsoft.Data.Sqlite`) in the app's private directory, in the plain .NET library
    `Kamada.Mobile.Storage` so it is unit tested without Android;
  - `INetworkEvents` → MAUI `Connectivity` plus `Window.Resumed`;
  - `IAudioRecorder` → Android `AudioRecord` at 16 kHz mono PCM16 (no resampling), `RECORD_AUDIO` asked on first use.
- **External links** (`target="_blank"`: Maps navigation) become plain navigations through a small script in the host
  page; `BlazorWebView` opens addresses outside the app with the system, so they start the Maps app. `tel:` links
  open the dialer the same way. The pages are unchanged.
- **Build-time API address** (`-p:KamadaApiBaseUrl=…`; Debug defaults to the emulator's `10.0.2.2:5080`), plain HTTP
  only in Debug builds. The API sees the app as a .NET client: no CORS entry, no CSP.
- **Separate solution.** The MAUI project needs the `android` workload, so it lives in `Kamada.Mobile.slnx`;
  `Kamada.slnx` (Docker images, the main CI job) builds without it. A CI job builds the APK; tags `v*` sign it with the
  keystore from repository secrets and attach it to a GitHub release.
- **The PWA is removed** (service, port 5082, service worker, IndexedDB and browser network code).

## Consequences

- Tokens are encrypted at rest on the phone; the offline copy is in app-private storage that the system does not evict.
  Dictation and navigation feel native.
- Workers install an APK (sideloaded or through a store later) and updates are versioned releases instead of a reload.
  iOS is possible with the same code but needs a Mac build agent – not done.
- Release builds are trimmed: a framework component referenced only from XAML lost its constructor (`HeadOutlet`, now
  dropped). New root components or reflection-only types must be checked in a Release build on a device.
- The admin web panel stays a Blazor WebAssembly app with tokens in `localStorage` (G11 remains accepted there).
