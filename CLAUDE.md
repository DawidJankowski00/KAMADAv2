# CLAUDE.md — KAMADA v2

Portfolio project (junior/mid .NET): order management + courier delivery routes with route optimization.
Code is written as if reviewed by a senior engineer: clear architecture, tests, docs, one-command startup.

## Product in one paragraph

**Admin** (browser, Blazor WASM + MudBlazor) manages products, customers, orders and payments, defines route
locations (warehouses/bases), creates delivery runs, assigns orders and a courier, and optimizes stop order.
**Courier** (phone, Blazor WASM PWA) sees own runs, stop order, opens the whole run in Google Maps and marks
stops as delivered / failed. The route optimizer fetches a **travel-time matrix** (OSRM / Google Routes / Haversine)
and solves a fixed-start/fixed-end TSP locally (brute force / Held-Karp / NN + 2-opt). We never scrape Google Maps.

## Tech stack

- .NET 10 (SDK pinned in `global.json`: 10.0.401, `rollForward: latestFeature`), C# latest, nullable on.
- `TreatWarningsAsErrors=true` for production projects (`src/**`), relaxed in `tests/**` and `benchmarks/**`.
- Central package management: `Directory.Packages.props`. Common settings: `Directory.Build.props`.
- Solution file: `Kamada.slnx`.
- ASP.NET Core **Minimal APIs**, grouped per module (`MapGroup("/api/<module>")`), one static `XxxEndpoints` class per feature area.
- EF Core 10 + Npgsql, PostgreSQL 17. Snake_case naming via `EFCore.NamingConventions`.
- Auth: JWT access token (15 min) + rotating refresh token (stored hashed in DB). Roles `Admin`, `Courier`.
  Policies `AdminOnly`, `CourierOnly`. Passwords: `PasswordHasher<T>` from ASP.NET Core Identity.
- FluentValidation (input), Serilog (structured logs), built-in OpenAPI + Scalar UI (`/scalar`).
- `Microsoft.Extensions.Http.Resilience` for outbound HTTP (OSRM, Nominatim, Google).
- Tests: xUnit v3 (4.x, runs on Microsoft.Testing.Platform — no `Microsoft.NET.Test.Sdk`), Shouldly, Testcontainers.PostgreSql, `WebApplicationFactory`, NetArchTest.Rules, Bogus.
- Benchmarks: BenchmarkDotNet (`benchmarks/Kamada.Benchmarks`).
- Frontends: Blazor WASM, MudBlazor, Leaflet via JS interop (admin map). Served by nginx in Docker.
- No MediatR / AutoMapper (licensing + unnecessary indirection): plain handler classes and explicit mapping.

## Architecture: modular monolith

One API host (`src/Kamada.Api`), eight modules. Each module = **two projects**:

```
src/Modules/<Module>/Kamada.Modules.<Module>/            # internal implementation
    Domain/          # entities, value objects, domain errors, state transitions — no EF/HTTP deps
    Application/     # use-case handlers, validators, ports (interfaces)
    Infrastructure/  # DbContext, EF configurations, migrations, external clients
    Api/             # Minimal API endpoint mappings
    <Module>Module.cs  # public: AddXxxModule(IServiceCollection, IConfiguration) + MapXxxEndpoints()
src/Modules/<Module>/Kamada.Modules.<Module>.Contracts/  # public API for OTHER modules (interfaces + DTOs)
```

Rules (enforced by `tests/Kamada.ArchitectureTests`):
- Everything inside a module project is `internal` except the module registration class; tests use `InternalsVisibleTo`.
- A module may reference **only the `.Contracts` project** of another module, never its implementation project.
- `Domain` namespace does not depend on `Infrastructure`, `Api`, EF Core or ASP.NET Core.
- Frontends reference only `Kamada.Shared.Contracts` (HTTP DTOs) and `Kamada.Web.Shared` (shared Blazor UI
  library, which itself references only `Kamada.Shared.Contracts`), never modules.

Modules: `Catalog`, `Customers`, `Ordering`, `Payments`, `Workforce`, `Delivery`, `RouteOptimization`, `Assistant`.
Shared: `Kamada.Shared.Kernel` (Result, Error, Entity/AggregateRoot base, domain events, clock abstractions),
`Kamada.Shared.Infrastructure` (EF interceptors, shared connection/transaction, ProblemDetails mapping),
`Kamada.Shared.Contracts` (HTTP request/response DTOs used by API and both Blazor apps, folder per module).

### Persistence

- **One PostgreSQL database, one schema per module, one `DbContext` per module**
  (`catalog`, `customers`, `ordering`, `payments`, `workforce`, `delivery`). Each context has its own
  migrations and its own `__ef_migrations_history` table in its schema. A context maps only its own tables.
- Cross-module atomic operations (e.g. create order + decrement stock) share one scoped `NpgsqlConnection`
  and one DB transaction via `IUnitOfWork` from `Kamada.Shared.Infrastructure`; each module context enlists
  in it (`Database.UseTransaction`). Documented in ADR.
- Stock (`stock_quantity`) lives in `catalog.products` (as in the diagram); it is mutated **only** through
  `ICatalogInventory` (Catalog.Contracts), called by Ordering.
- Cross-module references are plain IDs (no FK navigation across schemas; FK constraints only within a schema).
- Ids: `Guid` v7 (`Guid.CreateVersion7()`), generated in the domain.
- `created_at` / `updated_at` set by an EF `SaveChanges` interceptor using `TimeProvider`. Concurrency: `xmin`.
- Enums stored as strings. Money: `decimal(10,2)`. Timestamps: `timestamptz`, always UTC.

### Domain & errors

- State transitions are methods on entities (`order.MarkPaid()`, `run.Start()`), no public setters.
- Business failures return `Result` / `Result<T>` with a typed `Error` (`Validation`, `NotFound`, `Conflict`,
  `Forbidden`). Endpoints map them to `ProblemDetails` (RFC 9457): invalid transition → `409`.
- Unexpected exceptions → global `IExceptionHandler` → `500` ProblemDetails (no stack traces outside Development).

### Route optimization (key technical part)

- `ITravelTimeMatrixProvider` → `TravelMatrix` (durations + distances, may be asymmetric).
  Implementations: `OsrmMatrixProvider` (default), `GoogleRoutesMatrixProvider` (only with API key),
  `HaversineMatrixProvider` (tests + fallback; result flagged as approximate). Matrix cache keyed by
  sorted coordinates rounded to 5 decimals.
- `IRouteSolver.Solve(matrix, startIndex, endIndex)`; `RouteSolverSelector`: n ≤ 8 brute force,
  n ≤ 15 Held-Karp, otherwise nearest neighbour + 2-opt (+ Or-opt) with a time limit (non-optimal flag).
- `GoogleMapsUrlBuilder`: official Maps URLs format, coordinates, `%7C`-encoded waypoints, splits into
  chained segments when waypoint limit (config `GoogleMaps:MaxWaypointsPerLink`) is exceeded.

## Conventions

- Code, identifiers, commit messages, README, ADRs: **English**. UI texts in the apps: **Polish**.
- Conventional Commits (`feat(ordering): ...`, `test(delivery): ...`, `chore: ...`, `docs: ...`).
- File-scoped namespaces, `sealed` by default, primary constructors for DI, `CancellationToken` on all async APIs.
- One public type per file. Async methods end with `Async`. No `#region`.
- API routes kebab-case (`/api/delivery-runs/{id}/apply-optimization`), JSON camelCase, enums as strings.
- Validation of request shape in FluentValidation; business rules in domain.
  Endpoints opt in with `.WithValidation<TRequest>()` (endpoint filter, returns `400 ValidationProblem`).
- Application layer talks to persistence via `IXxxDbContext` (port in `Application/Abstractions`, DbSets +
  `SaveChangesAsync`) and EF LINQ — no generic repositories. EF Core is allowed in Application, never in Domain.
- Use cases: simple CRUD grouped in `XxxQueries` / `XxxCommands` classes; non-trivial flows get a dedicated
  `XxxHandler` (e.g. `LoginHandler`). All return `Result`/`Result<T>`; endpoints map with `ToHttpResult(...)`.
- Endpoints are secure by default (fallback policy = authenticated). Public ones call `.AllowAnonymous()`.
  Current worker: inject `ICurrentUser`.
- Domain entities: private constructor + static factory, `Guid.CreateVersion7()`, private setters,
  implement `IAuditable` for `created_at`/`updated_at`. EF configs in `Infrastructure/Persistence/Configurations`
  must `Ignore(e => e.DomainEvents)`.
- Demo data: `IDataSeeder` per module (`Infrastructure/Seeding`), run in `SeedOrder` in one scope only when
  `Database:SeedDemoData`; each seeds only into empty tables. Business data goes through the real use cases, ids are
  handed on via `DemoSeedData`, history is replayed with `SeedClock` (see ADR 0007). Integration tests disable seeding
  and arrange their own data (see `tests/Kamada.IntegrationTests/Infrastructure`); `DemoDataSeedingTests` seeds a
  separate database.
- Config via options pattern (`IOptions<T>` + `ValidateOnStart`). No secrets in repo: user secrets / env vars,
  `.env.example` documents them.
- Tests: `MethodName_Scenario_ExpectedResult` naming; integration tests use Testcontainers (Docker required),
  matrix provider replaced with Haversine, geocoding replaced with a fake.

## Commands

```bash
dotnet build Kamada.slnx
dotnet test --solution Kamada.slnx     # Microsoft.Testing.Platform runner (opted in via global.json)
dotnet test --project tests/Kamada.ArchitectureTests
docker compose up --build              # full stack
docker compose up -d postgres          # DB only, for local dev
docker compose --profile osrm up       # + self-hosted OSRM (set OSRM_BASE_URL/OSRM_MIN_DELAY in .env)
docker compose --profile ai up         # + Ollama for the order assistant (set ASSISTANT_ENABLED=true in .env)
docker compose down -v                 # reset the database (demo data is seeded again)
dotnet run --project src/Kamada.Api
dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --filter '*'   # solver timings
dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --quality      # heuristic gap vs optimum
dotnet run -c Release --project benchmarks/Kamada.Assistant.Evals -- --model gemma3:4b   # assistant accuracy (needs Ollama + Postgres)
```

EF migrations (per module context; `dotnet-ef` is a local tool: `dotnet tool restore`), e.g.:

```bash
dotnet ef migrations add Initial -p src/Modules/Catalog/Kamada.Modules.Catalog -s src/Kamada.Api -c CatalogDbContext -o Infrastructure/Persistence/Migrations
```

Migrations are generated code (`.editorconfig` disables analyzers for `**/Migrations/*.cs`). On a fresh
database EF logs one `Failed executing DbCommand ... __ef_migrations_history` error per module on first start —
benign, EF probes the history table before creating it.

## Ports (local / docker)

| Service | URL |
|---|---|
| API (+ `/scalar`, `/health`) | http://localhost:5080 |
| Admin panel | http://localhost:5081 |
| Courier app | http://localhost:5082 |
| PostgreSQL | localhost:5432 |
| OSRM (optional profile) | http://localhost:5000 |
| Ollama (optional profile `ai`) | http://localhost:11434 |

## Minor decisions log

Record small decisions here (date — decision — reason).

- 2026-09-28 — Minimal APIs over controllers — less ceremony, natural grouping per module.
- 2026-09-28 — Two projects per module (impl + Contracts) with internal layers as folders — enforces
  boundaries via `internal` + NetArchTest without 30+ projects.
- 2026-09-28 — Shouldly over FluentAssertions — FluentAssertions v8 is commercially licensed.
- 2026-09-28 — No MediatR — commercial license since v13; plain handlers are enough.
- 2026-09-28 — `GoogleMaps:MaxWaypointsPerLink` default 9 — verified in the Maps URLs docs: up to 3 waypoints in
  mobile browsers, 9 otherwise (couriers open links in the Google Maps app). Long routes split into evenly sized parts.
- 2026-09-28 — `dotnet test` uses Microsoft.Testing.Platform (`global.json` → `test.runner`) — required by
  xUnit v3 4.x on .NET 10 SDK; test projects are executables (`OutputType=Exe`, `tests/Directory.Build.props`).
- 2026-09-28 — Integration tests share one PostgreSQL Testcontainer per test assembly
  (`[assembly: AssemblyFixture(typeof(KamadaApiFactory))]`); the connection string is injected with
  `UseSetting` so it is visible to code reading configuration before `builder.Build()`.
- 2026-09-28 — Architecture tests read `<ProjectReference>` items from .csproj files in addition to
  NetArchTest, because unused assembly references are dropped by the compiler.
- 2026-09-28 — Test projects don't use `TreatWarningsAsErrors` (overridden in `tests/Directory.Build.props`).
- 2026-09-28 — Single `/api/auth/login` for both apps; each frontend rejects a token with the wrong role,
  the API enforces roles via policies anyway.
- 2026-09-28 — Login rate limit: fixed window per client IP (`RateLimiting:Login`), tests raise the limit and
  verify 429 on a separate host (`WithWebHostBuilder`) so limiter state is isolated.
- 2026-09-28 — Serilog registered with `AddSerilog` (no static bootstrap logger) so multiple test hosts work.
- 2026-09-28 — Npgsql connection strings use `Gss Encryption Mode=Disable` (no Kerberos in containers).
- 2026-09-28 — Geocoding lives in `Kamada.Shared.Infrastructure/Geocoding` (used by Customers and later Delivery),
  `GeoPoint` in Shared Kernel. Provider via `Geocoding:Provider` (`Nominatim` default / `Google` / `None`).
  Nominatim: identifying User-Agent, `RequestThrottle` inside the resilience pipeline (every retry throttled),
  results cached (not-found for 10 min). Provider failures never fail a save — address stored without coordinates.
  Integration tests replace `IGeocodingService` with `FakeGeocodingService`.
- 2026-09-28 — Addresses are archived (not deleted) and clients/products deactivated, because orders reference them.
  Changing only apartment number or address type keeps coordinates; changing the building clears and re-geocodes.
- 2026-09-28 — Stock lives in `catalog.products.stock_quantity`, changed only via `Product` methods
  (`SetStock` by admin, `ReserveStock`/`ReleaseStock` for Ordering in stage 4). DB check constraints back the invariants.
- 2026-09-28 — Category filter on products includes subcategories (tree resolved in memory — `CategoryTree`).
  Sibling category names unique via index with `NULLS NOT DISTINCT` (root level too).
- 2026-09-28 — HTTP route for Customers module is `/api/clients` (matches table name `clients`).
- 2026-09-28 — `UseSerilogRequestLogging` is registered before `UseExceptionHandler` so it logs final status codes.
- 2026-09-28 — Cross-module writes: `IUnitOfWork.ExecuteAsync` (one shared `NpgsqlConnection` per scope, all module
  contexts enlisted in one transaction, commit only on success). Never run queries of two contexts in parallel
  within a request (they share the connection).
- 2026-09-28 — Inter-module contracts so far: `ICatalogInventory` (Catalog: reserve/release stock, all-or-nothing,
  validates before mutating), `ICustomerDirectory` (Customers: shipping details, current address coordinates),
  `IOrderPayments` (Ordering: order state for payments, mark paid).
- 2026-09-28 — Orders snapshot recipient + address (+ coordinates) as an EF complex property; `shipping_address_id`
  keeps the reference. Delivery (stage 7) should read *current* coordinates via `ICustomerDirectory.GetAddressLocationsAsync`
  (an admin may pin the address after the order was placed), falling back to the snapshot.
- 2026-09-28 — Order lifecycle: cancel allowed in New/Paid/ReadyForDelivery (returns stock); refunds are a separate
  Payments action allowed only for cancelled orders. `DeliveryFailed → ReadyForDelivery` allows re-planning.
- 2026-09-28 — Payments: simulated gateway — Cash approved, Card approved (or declined with `simulateDecline`),
  Transfer pending until `POST /api/payments/{id}/confirm`. One active (Pending/Completed) payment per order
  (partial unique index). Declined attempts are stored as `Failed`.
- 2026-09-28 — `UseXminConcurrencyTokens` only touches entity types with a primary key (complex types discovered by
  convention must not be promoted to entities).
- 2026-09-28 — xUnit theories cannot take internal enums as parameters (test methods are public): pass
  `nameof(Enum.Value)` strings and `Enum.Parse` inside the test.
- 2026-09-28 — Frontend plumbing lives in `src/Web/Kamada.Web.Shared` (RCL): `ApiClient` (typed calls → `ApiResult`,
  ProblemDetails parsing, Polish messages for known error codes in `ErrorMessages`), `AuthService` (login with role
  check per app, refresh-token rotation with a lock, session in localStorage via `TokenStore`), `AuthorizingHandler`
  (bearer + one retry after refresh on 401), `SessionAuthenticationStateProvider`, `LeafletMap` component
  (Leaflet 1.9.4 from jsDelivr, OSM tiles, markers/route/click-to-pin), `Labels` (Polish labels, money, dates).
  Auth-related services are singletons (WASM has one scope, and HttpClient handlers get their own scope).
- 2026-09-28 — Admin panel UI: MudBlazor 9 (API ≈ v8: `IMudDialogInstance`, `ServerData(TableState, CancellationToken)`,
  `MudText HtmlTag`, `SelectedValues` is `IReadOnlyCollection<T>`; use `ToStringFunc` instead of
  `MultiSelectionTextFunc`). Grey helper text: `Class="mud-text-secondary"` (`Color.Secondary` is the purple accent).
  Coordinates are always shown with invariant decimals (`Labels.Coordinates`).
- 2026-09-28 — Frontends in Docker: nginx (`deploy/nginx/spa.conf`, SPA fallback, `gzip_static`), API address injected
  at container start by `deploy/nginx/40-kamada-config.sh` from `API_BASE_URL` (compose: `PUBLIC_API_URL`).
- 2026-09-28 — Local UI testing: `.claude/launch.json` has `admin-panel` (5081) and `courier-app` (5082) dev servers;
  API runs from `docker compose up -d` (5080). Stop the dockerized `admin` service before starting the dev server on 5081.
- 2026-09-28 — RouteOptimization is stateless (no schema/DbContext). Contracts: `IRoutePlanner` (`OptimizeAsync` with an
  optional current order evaluated on the *same* matrix, `EvaluateAsync` for manual orders; stop indexes refer to
  `RoutePlanRequest.Stops`) and `INavigationLinkBuilder` (Google Maps links as already-encoded strings – `Uri.ToString()`
  would unescape `%7C`). Solvers and `TravelMatrix` live in `Domain` (pure, no I/O).
- 2026-09-28 — Matrix provider pipeline: `CachingTravelMatrixProvider` → `FallbackTravelMatrixProvider` → primary
  (`RouteOptimization:MatrixProvider` = `Osrm` | `Google` | `Haversine`). Fallback catches only provider failures
  (HTTP, Polly timeout/circuit, bad payload), never caller cancellation or bugs. Approximate matrices are not cached.
  Pairs a provider cannot route are estimated in a straight line and the whole matrix is flagged approximate.
- 2026-09-28 — Public OSRM demo server is the dev default (throttled to 1 request/s via `RequestThrottle`, now in
  `Kamada.Shared.Infrastructure/Http`); the self-hosted OSRM container is the compose profile `osrm`.
- 2026-09-28 — `/health` includes `travel-matrix` (primary provider, uncached): failure reports `Degraded` (HTTP 200),
  because planning still works with the fallback; the probe result is cached for 5 minutes.
- 2026-09-28 — Heuristic = nearest neighbour or input order (whichever is cheaper) + 2-opt with O(1) asymmetric
  reversal deltas (forward/backward prefix sums) + Or-opt (chains of 1–3); time limit `RouteOptimization:HeuristicTimeLimit`.
  Unit tests use `FakeTimeProvider` so it always converges (deterministic).
- 2026-09-28 — Google Routes matrix: `TRAFFIC_UNAWARE`, 25 × 25 tiles (625 elements per request); proto3 JSON omits
  zero values, so missing `originIndex`/`destinationIndex`/`distanceMeters` mean 0.
- 2026-09-28 — Delivery uses `IOrderFulfillment` (Ordering: delivery view of orders + bulk all-or-nothing status transitions)
  and `ICourierDirectory` (Workforce: assignable = active worker with the Courier role). Order and stop changes are
  saved in one `IUnitOfWork` transaction.
- 2026-09-28 — "One order in one active run" is enforced by the order lifecycle (`ReadyForDelivery → AssignedToRun`
  in the same transaction, `xmin` on the order), not by an index: an order may appear in several runs over time
  (failed → re-planned).
- 2026-09-28 — Runs are editable only in `Draft`; adding/removing stops or changing endpoints clears the stored plan,
  approval requires a plan. `apply-optimization` re-optimizes on the server (matrix is cached) instead of trusting an
  order sent by the client. Manual order (`PUT sequence`) is stored with algorithm `Manual`.
- 2026-09-28 — `(delivery_run_id, delivery_sequence)` unique via a `DEFERRABLE INITIALLY DEFERRED` constraint created
  in the migration SQL, so stops can swap positions in one `SaveChanges`. `google_maps_urls` is jsonb via a value converter.
- 2026-09-28 — Arrival estimates = driving time + `Delivery:ServiceTimePerStop` (default 3 min) at earlier stops;
  run totals stay pure driving time/distance. Stops store the coordinates the plan was computed with.
- 2026-09-28 — Courier API under `/api/courier/delivery-runs` (policy `CourierOnly`): other couriers' runs, drafts
  and cancelled runs return 404. "Today" for the Current/Upcoming lists uses `Delivery:TimeZone` (Europe/Warsaw).
  Completing a run early fails pending stops with `DeliveryRun.NotVisitedReason` (Polish text stored as data).
- 2026-09-28 — The public OSRM demo server returns 403 without a User-Agent: `RouteOptimization:Osrm:UserAgent`.
- 2026-09-28 — Admin run planning UI (`Pages/DeliveryRuns`): run details page is the workspace (add orders, optimize,
  reorder, approve, cancel). Manual order: `MudDropContainer` + up/down buttons, kept locally until "Zapisz kolejność"
  (`PUT sequence`); the container is re-keyed after each move so it always renders the list order. The optimization
  dialog only previews (`POST optimize`) and applies via `apply-optimization`. Map route = straight segments
  (no road geometry, the navigation link shows the real road). Parameterised API errors (order numbers) get generic Polish
  messages; the page shows per-stop coordinate warnings itself.
- 2026-09-28 — `QueryString` formats `DateOnly` as `yyyy-MM-dd` (invariant `DateOnly.ToString()` is `MM/dd/yyyy`).
  Formatting helpers of `Kamada.Web.Shared` are unit tested in `tests/Kamada.Web.Shared.UnitTests`.
- 2026-09-28 — Courier PWA (`src/Web/Kamada.CourierApp`): phone-first MudBlazor UI, same `AddKamadaClient` plumbing as the admin
  panel (role `Courier`, own localStorage key). Run list tab is kept in the URL (`?scope=`) so "back" returns to it; each
  action (start / deliver / fail / complete) returns the updated run, a 404/409 reloads it (e.g. cancelled by the admin).
  Delivering asks for confirmation; failure reasons offer common presets. Snackbars at the top (`KamadaClientOptions.SnackbarPosition`),
  because the bottom of a phone screen holds the stop action buttons. `DeliveryRunMap`, `NavigationLinks` and `RunStatusChip`
  moved to `Kamada.Web.Shared` (used by both apps).
- 2026-09-28 — Courier service worker: `appsettings.json` is excluded from the offline precache (the container rewrites it, so the
  published integrity hash would fail the install) and fetched network-first with the last copy as offline fallback. API calls
  are never cached – stop outcomes must reach the server.
- 2026-09-29 — Demo seed: seeders hand generated ids to later seeders through the scoped `DemoSeedData` instead of
  deterministic ids or seeding-only contract queries (domain keeps generating Guid v7, contracts stay production API).
  Past moments are replayed with `SeedClock` via `ActivatorUtilities.CreateInstance(..., seedClock)`; `created_at` is
  backdated with `ExecuteUpdate` in the owning module, followed by `ChangeTracker.Clear()` (the update changes `xmin`).
  Demo times are Polish local time (`DemoSeedData.TimeZone`) and must be converted to UTC before writing (Npgsql).
- 2026-09-29 — Demo clients: real buildings in the Tricity and Szczecin geocoded once with Nominatim (coordinates stored in
  `CustomersSeeder`); names from curated lists of common Polish names (Bogus' `pl` locale produced rare names and
  surnames of the wrong gender), picked with a fixed Bogus seed.
- 2026-09-29 — Self-hosted OSRM: compose profile `osrm` (`osrm-download`: curl + `osmium merge` of the
  Geofabrik voivodeship extracts in `OSRM_REGIONS` → `osrm-prepare` MLD preprocessing → `osrm-routed`), data kept in the
  `osrm-data` volume; a marker file with the region list re-runs merge and preprocessing when the list changes.
  The API keeps the public demo server as default; `.env` switches it (`OSRM_BASE_URL`, `OSRM_MIN_DELAY=00:00:00`).
- 2026-09-29 — Demo company delivers in two areas (`DemoRegion`): the Tricity (Pomorskie; warehouse Gdańsk, base Gdynia,
  `kurier1`) and Szczecin (Zachodniopomorskie; `kurier2`). Orders alternate between areas so both get runs; runs never
  mix areas. Default OSRM map: Pomorskie + Zachodniopomorskie + Wielkopolskie + Kujawsko-Pomorskie (connected, ~480 MB);
  `osrm-download` uses `debian:trixie-slim` because the OSRM image (Debian 11) is past its support window for `apt`.
- 2026-10-10 — Order assistant (ADR 0008): new stateless `Assistant` module (references only Customers/Catalog contracts).
  Ollama via `Microsoft.Extensions.AI` `IChatClient` (OllamaSharp, keyed service), JSON schema + temperature 0; the model
  returns mentions only, matching is deterministic C# (`TextSimilarity` in Shared Kernel: folding incl. `ł`, Jaro-Winkler,
  length-weighted phrase score). Contracts: `ICustomerDirectory.FindClientCandidatesAsync` / `FindShippingAddressAsync` /
  `SuggestUsernameAsync`, `ICatalogLookup.FindProductCandidatesAsync` (in-memory over narrow projections).
- 2026-10-10 — Draft thresholds live in `OrderDraftResolver`: client selected on exact phone/e-mail or a single name match
  ≥ 0.92 (namesakes broken by a matching address); product selected at ≥ 0.85 with a 0.05 margin over the runner-up
  (tuned down from 0.9 after the evals: a fully covered phrase plus extra catalogue words scores 0.85–0.9). An existing
  address matches on street + building + apartment; the city only breaks ties (models sometimes change the city).
  Geocoder output replaces spoken street/city only when similar (≥ 0.85), otherwise the lookup is ignored with a warning.
- 2026-10-10 — Accepting a draft = existing endpoints in sequence from the admin panel (create client with address / add
  address → place order), no cross-module transaction; the page switches to "existing" after each create, so retries do
  not duplicate. A client created for a failed order is acceptable data.
- 2026-10-10 — `Assistant:Enabled` (default off, on in Development): when off, inference endpoints return 503
  `Assistant.Disabled`, `GET /api/assistant/status` reports it and the panel hides; the `assistant` health check is
  registered only when enabled (`Degraded` on failure). Rate limit policy `assistant` is per user (`sub`), configurable as
  `RateLimiting:Assistant`. New `ErrorType.Unavailable` → 503.
- 2026-10-10 — Integration tests replace the model with `FakeOrderExtractor` (extraction registered per unique note text)
  and `FakeLanguageModelProbe`; `FakeGeocodingService` fills a missing postal code (`80-001`).
- 2026-10-10 — Demo catalogue has "Napoje" and "Pieczywo" (water still/sparkling, juices, bread) so the example dictations
  resolve, including an ambiguous "woda".
- 2026-10-10 — Dictation: `VoiceRecorder` (Web.Shared) captures with an AudioWorklet and resamples to 16 kHz mono in the
  browser (`OfflineAudioContext`); JS returns 16-bit PCM and `WavEncoder` (C#, unit tested) writes the header. The server
  validates the WAV (`WavAudio`: PCM 16 kHz mono 16-bit, ≤ 60 s, ≤ 2 MB) before Whisper. No Web Speech API (Chrome sends
  audio to Google). Minimal API multipart endpoint uses `DisableAntiforgery()` (bearer auth).
- 2026-10-10 — `WhisperSpeechToText` (Whisper.net 1.9, singleton): the GGML model is downloaded once to
  `Assistant:Whisper:ModelPath` (temp file + move) by the `SpeechModelWarmUp` background service; until loaded, status reports
  `speechToText: false` and the panel polls every 15 s. `SemaphoreSlim(MaxConcurrency)`; threads = half the cores (≤ 8).
  The API image needs `libgomp1` (OpenMP) for the linux-x64 natives and a writable `/app/models` volume.
- 2026-10-10 — Geocoding: Nominatim retries a street without a type word as `aleja …` (OSM naming); the assistant trusts a
  geocoder hit only for the same building number (street-level hits had a mid-street pin and possibly another postal code).
- 2026-10-10 — Ollama in compose: `OLLAMA_KEEP_ALIVE=30m` (reloading `gemma3:4b` on CPU takes ~50 s). The fixed system prompt +
  few-shot prefix is reused by Ollama's prompt cache, so a warm request evaluates only the note (~11–20 s on a laptop CPU).
- 2026-10-10 — `Assistant:Ollama:Think` (optional): Ollama's `think` has no `IChatClient` equivalent, so the extractor passes
  `ChatOptions.RawRepresentationFactory = _ => new ChatRequest { Think = … }` (OllamaSharp starts from that native request).
  Left unset for non-reasoning models (setting it may fail there); `false` for qwen3 (otherwise hundreds of thinking tokens).
- 2026-10-10 — Assistant evals (`benchmarks/Kamada.Assistant.Evals`): the whole API in-process via `WebApplicationFactory`
  (content root through `UseSolutionRelativeContentRoot(..., "Kamada.slnx")` – the default lookup expects `*.sln`), its own
  database `kamada_evals` seeded with demo data, geocoding off, real Ollama model from `--model`. Cases in `cases.json`
  (expected client/address decisions, SKUs or "ambiguous"/"not found", quantities); lines are compared by position.
- 2026-10-10 — Default model stays `gemma3:4b` after the evals (README table): as accurate as Bielik 4.5B, 2–3× faster, the
  only one usable on a laptop CPU. On a GPU any of them answers in 1–2 s, so `OLLAMA_BASE_URL` may point to another
  machine in the LAN (evals: `--ollama http://<host>:11434/`).
- 2026-10-11 — `KamadaApiFactory` starts the shared host in `InitializeAsync`: hosts built later with `WithWebHostBuilder` then
  find the database migrated. EF's migration lock is held per migration transaction, so two hosts starting together could
  both apply a module's second migration.
- 2026-10-11 — Workers have a set of roles (`workforce.workers.roles text[]`, enum names, check `cardinality(roles) > 0`,
  EF primitive collection over the `_roles` field, kept in enum order). Domain rules in `Worker.SetRoles`: at least one role,
  no duplicates, nobody removes their own `Admin` role (`Workers.CannotRemoveOwnAdminRole`, 409). The workers list filter
  `role` means "has this role". `ICourierDirectory` treats any active worker with the Courier role as assignable.
- 2026-10-11 — One `role` claim per role in the access token; `AuthenticatedUserDto.Roles`. Role changes apply at the next
  refresh (≤ 15 min). Frontends declare `KamadaClientOptions.AllowedRoles` (admin panel `[Admin]`, courier app `[Courier]`);
  a refresh without any allowed role signs out, a refresh with changed roles raises `SessionChanged`. Sessions stored in the
  old single-role format are dropped by `TokenStore` (the user signs in again).
