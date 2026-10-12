# 0005. Two Blazor WebAssembly frontends with a shared UI library

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

There are two very different users: an **admin** at a desktop (tables, forms, a map, drag and drop) and a
**courier** on a phone (a few big buttons, used outdoors, sometimes on a weak connection). Both talk to the same
API and share a lot of plumbing: login with refresh tokens, error handling, Polish labels, the delivery run map.
The project is a .NET system, so the frontends should show C# end to end and share types with the API.

## Decision

- **Two Blazor WebAssembly apps**, both with MudBlazor: `Kamada.AdminPanel` (desktop-first) and
  `Kamada.CourierApp` (phone-first, installable PWA). Separate apps because the audiences, layouts and roles
  differ; one app with two layouts would ship admin code to every courier phone.
- **Shared Razor class library `Kamada.Web.Shared`**: `ApiClient` (typed calls returning `ApiResult<T>`,
  ProblemDetails parsing, Polish messages per error code), `AuthService` + `AuthorizingHandler` (token refresh
  with one retry on 401), `SessionAuthenticationStateProvider`, the Leaflet map component, delivery run
  map/navigation components and formatting helpers (`Labels`). Each app configures it with
  `AddKamadaClient(options)`: its required role, localStorage key and snackbar position.
- **DTOs from `Kamada.Shared.Contracts`** are used by the API and both apps, so request/response shapes cannot
  drift. Frontends never reference module projects (architecture test).
- **Static hosting with nginx** in Docker. The API address is injected at container start
  (`deploy/nginx/40-kamada-config.sh` rewrites `appsettings.json` from `API_BASE_URL`), so one image works in
  every environment. `index.html`, `appsettings.json` and the service worker files are served with
  `no-cache`, fingerprinted framework files are cached forever.
- **Courier PWA service worker** caches the app shell for offline start, fetches `appsettings.json`
  network-first (it is rewritten per container, so it is excluded from the integrity-checked precache) and
  **never caches API calls**: a stop outcome must reach the server, not a cache.

## Alternatives considered

- **Blazor Server / Interactive Auto:** needs a permanent SignalR connection, a poor fit for couriers on
  mobile networks.
- **React/Angular + TypeScript:** a fine choice in general, but DTOs would be duplicated (or generated) and the
  project would show less .NET.
- **One app with role-based layouts:** smaller repository, but larger download for couriers and mixed concerns.

## Consequences

- First load downloads the .NET WebAssembly runtime (a few MB, compressed and cached afterwards).
- UI logic worth testing lives in plain C# helpers of `Kamada.Web.Shared`, covered by unit tests; components
  themselves are verified manually in the browser.
- Offline support is limited to starting the courier app; actions need a connection (an outbox with retry is a
  possible improvement).

## Update (2026-10-11): one phone app for every role

The courier PWA became the KAMADA phone app (`Kamada.MobileApp`), because workers now hold several roles and
switch between them on one phone (see `docs/plans/mobile-app-roles.md`). The split above still holds along a
different line: **desktop vs. phone**, not courier vs. admin. The web admin panel stays a separate app; the phone
app has one area per role (`/courier/...`, `/admin`), and the active role is a client-side choice of views
(`RoleContext`) while the API keeps checking roles per endpoint. Admin pages will be shared through a Razor class
library rather than copied, so the phone app ships them alongside the courier views (the larger download is accepted
for one app per worker).

Done in stage 3: the admin pages, dialogs and menu live in `Kamada.Web.Admin` (references only
`Kamada.Shared.Contracts` and `Kamada.Web.Shared`, enforced by the architecture tests). The web panel keeps only its
host (Program, App, layouts, login); both apps add the library to the router, so the pages are identical by
construction. Small screens are handled inside the shared pages with CSS (MudBlazor breakpoint classes, a full-screen
dialog class, map height variables, `pointer: coarse` for drag and drop), so the desktop layout does not change.
The admin home moved to `/dashboard`, because `/` in the phone app is the active role's home.
