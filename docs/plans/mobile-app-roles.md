# Plan: one phone app for Courier, Admin and Seller (+ offline voice order assistant)

Read `CLAUDE.md` first and follow its conventions (modular monolith rules, `Result`/`Error`, Minimal APIs, `internal`
everything, FluentValidation, Polish UI texts, English code, test naming, decisions log).

## Goal

The existing phone app (`src/Web/Kamada.CourierApp`, Blazor WASM PWA) becomes the single KAMADA phone app:

- A worker signs in once and **automatically lands in the view of their role**.
- A worker can have **several roles** and **switches between them in the app** without signing in again.
- New role views on the phone:
  - **Admin**: everything the web admin panel (`src/Web/Kamada.AdminPanel`) can do.
  - **Seller** (new role): creates orders (manually or with the voice assistant) and sees their own orders.
- **Voice order assistant on the device**: when the server or the internet connection is poor, speech-to-text and the
  LLM run on the phone, the draft is matched against a local copy of the catalogue and clients, and the order is queued
  and sent when the connection is back.

## Where we start (what the code does today)

- `Worker` has exactly one `WorkerRole` (`Admin` | `Courier`), stored as a string column; the JWT has one `role` claim;
  `AuthenticatedUserDto.Role` is a single string.
- Each frontend accepts only one role (`KamadaClientOptions.RequiredRole`, checked in `AuthService.LoginAsync`).
- All admin endpoint groups use `AuthorizationPolicies.AdminOnly`; couriers use `CourierOnly` under `/api/courier/...`.
- The admin panel pages (~4k lines of Razor) live in the `Kamada.AdminPanel` host project, so the phone app cannot reuse them yet.
- The assistant runs on the server (Whisper.net + Ollama); the deterministic matching is spread over `TextSimilarity`
  (Kernel), `ClientMatcher`/`AddressMatcher`/`UsernameSuggestion` (Customers), `ProductMatcher` (Catalog) and
  `OrderDraftResolver` (Assistant).
- The courier PWA already caches the app shell offline; API calls are never cached.
- Housekeeping: `main` includes `254a7bb fix(assistant): detect silent recordings…` (merged in PR #2). The `gh` CLI is not
  used: Dawid opens the PRs himself.

## Working rules (apply to every stage)

1. **One branch per stage**, created from an up-to-date `main` (`feat/mobile-<stage-name>`). The next stage starts
   after the previous PR is merged (no stacked PRs unless Dawid asks for it).
2. **Small steps, one commit each.** Every step ends with: `dotnet build Kamada.slnx` (warnings are errors),
   `dotnet test --solution Kamada.slnx` (or at least the affected test projects + architecture tests), a UI check in the
   browser preview at phone size (375×812) when the step changes UI, then a Conventional Commit
   (`feat(mobile): …`, `refactor(web): …`, `test(workforce): …`).
3. **No AI attribution anywhere**: no `Co-Authored-By` trailers, no "Generated with…" lines, no session links in
   commits or PR descriptions. Commits are authored by the configured git user only.
4. **Ask before every push.** Before `git push`, post in the thread:
   - the commit list (`git log --oneline origin/main..HEAD`),
   - a short summary of what was done and what was verified (tests, screens checked),
   - the PR title and description that will be opened.
   Then **wait for Dawid's explicit OK**. No push, no PR without it.
5. **A PR at the end of every stage** (title = stage name, body: Before / After / How, plus a manual test list).
6. Small decisions go into the `CLAUDE.md` decisions log in the same commit that makes them; bigger ones get an ADR.
7. EF migrations are generated per module context with `dotnet ef` (never hand-written), one migration per step.

---

## Stage 0: housekeeping (no PR of its own)

- 0.1 Done: `254a7bb` reached `main` in PR #2, so every stage branches from the real state.
- 0.2 No GitHub CLI: Dawid opens every PR himself after the push (from the compare link that `git push` prints).
- 0.3 Commit this plan: `docs: plan for the multi-role phone app` (first commit on `feat/mobile-multi-role`, so it reaches
  `main` with the stage 1 PR).

## Stage 1: multi-role worker accounts (backend + web admin) → PR "Multi-role worker accounts"

Branch `feat/mobile-multi-role`.

- 1.1 `refactor(workforce): worker holds a set of roles`
  `Worker.Roles` (read-only set) replaces `Role`; `HasRole(...)`; `SetRoles(roles, changedBy)` with domain rules:
  at least one role, no duplicates, a worker cannot remove their own `Admin` role (replaces `CannotChangeOwnRole`).
  Unit tests in `Kamada.Modules.Workforce.UnitTests`.
- 1.2 `feat(workforce): store worker roles as an array`
  Column `roles text[]` (enum values as strings, EF primitive collection) + migration that copies the old `role`
  column into the array and drops it; check constraint `cardinality(roles) > 0`. Seeder updated.
- 1.3 `feat(workforce): one role claim per role in the access token`
  `JwtTokenService` emits one `role` claim per role; `AuthenticatedUserDto.Role` → `Roles` (string array).
  Existing policies keep working (`RequireRole` matches any claim). Integration tests: a worker with
  Admin + Courier can call admin and courier endpoints; refresh keeps the roles; changing roles takes effect at the
  next refresh (≤ 15 min, documented).
- 1.4 `feat(workforce): workers API manages several roles`
  `WorkerDto.Roles`, `CreateWorkerRequest.Roles`, `UpdateWorkerRequest.Roles`, list filter "has role";
  FluentValidation (non-empty, known values). Integration tests.
- 1.5 `feat(web): frontends accept users by allowed roles`
  `KamadaClientOptions.RequiredRole` → `AllowedRoles`; login succeeds when the user has any allowed role;
  `SessionAuthenticationStateProvider` emits all role claims. Unit tests in `Kamada.Web.Shared.UnitTests`.
  Web admin: `AllowedRoles = [Admin]`; phone app: `[Courier]` for now.
- 1.6 `feat(admin-panel): assign several roles to a worker`
  Workers dialog: role multi-select; list shows role chips; filter by role.
- 1.7 `docs: multi-role accounts` (decisions log, note in ADR 0003 about role claims).
- → **Ask for push OK** → push → PR.

## Stage 2: phone app shell with role-based views and role switching → PR "Phone app: role views and role switching"

Branch `feat/mobile-shell`.

- 2.1 `refactor(mobile): rename the courier app to the KAMADA phone app`
  `Kamada.CourierApp` → `Kamada.MobileApp` (project, namespaces, `Kamada.slnx`, compose service, nginx, `.claude/launch.json`
  config renamed to `mobile-app` on port 5082, architecture tests, README). Manifest name "KAMADA". The session is read
  once from the old localStorage key, so signed-in couriers stay signed in. No behaviour change.
- 2.2 `feat(web): active role service`
  `RoleContext` (Web.Shared, plain C#, unit tested): available roles = user roles ∩ roles the app supports; the active role
  is remembered per user in localStorage; default = the only role, otherwise the last used one, otherwise the first in
  the order Seller → Courier → Admin (most common daily work first). Raises `ActiveRoleChanged`.
- 2.3 `refactor(mobile): courier pages under /courier`
  `/courier`, `/courier/runs/{id}`; `/` becomes `RoleHome`, which redirects to the home of the active role.
  Pages are guarded with `[Authorize(Roles = ...)]`; opening a page of another role you have offers "Przełącz na …".
- 2.4 `feat(mobile): role switcher`
  App-bar menu "Rola: Kurier ▾" (shown only with 2+ roles); switching navigates to that role's home without
  signing in again; per-role bottom navigation and accent colour so the current role is obvious.
- 2.5 `feat(mobile): sign in to any phone role`
  `AllowedRoles = [Courier, Seller, Admin]`, generic login texts, demo accounts listed per role.
- 2.6 `chore(seed): demo worker with several roles` (e.g. `kierownik`: Admin + Courier).
- 2.7 `docs: phone app roles` (decisions log; README section "Aplikacja na telefon").
- → **Ask for push OK** → push → PR.

## Stage 3: Admin role on the phone (parity with the web panel) → PR "Phone app: admin role"

Branch `feat/mobile-admin`. Parity comes from **sharing the same pages**, not from rewriting them.

- 3.1 `refactor(web): move admin pages to a shared Razor class library`
  New `src/Web/Kamada.Web.Admin` (references only `Kamada.Shared.Contracts` and `Kamada.Web.Shared`, enforced by the
  architecture tests). Pages, dialogs, `PageHeader`, `OrderStatusChip`, `CategoryOptions` move there; `Kamada.AdminPanel`
  keeps only Program, App, layouts, login and nav. The dashboard route becomes `/dashboard` (the web panel redirects `/`
  to it; on the phone `/` is the role home). Web panel checked page by page, no behaviour change.
- 3.2 `refactor(web): one admin navigation model`
  `AdminNavigation` (sections, links, icons) used by the web `NavMenu` and by the phone drawer, so the two cannot drift.
- 3.3 `feat(mobile): admin area`
  Router `AdditionalAssemblies` includes `Kamada.Web.Admin`; phone admin layout with a drawer built from
  `AdminNavigation`; admin pages require the `Admin` role and an active Admin role.
- 3.4 `feat(web-admin): phone layout for orders` (orders list as cards below `sm`, order details, payment dialog full screen on phones).
- 3.5 `feat(web-admin): phone layout for clients and addresses` (incl. the pin-location map dialog).
- 3.6 `feat(web-admin): phone layout for products and categories` (category tree, stock dialog).
- 3.7 `feat(web-admin): phone layout for delivery runs and route locations` (map height, reorder with the existing
  up/down buttons instead of drag and drop on touch, optimization dialog full screen).
- 3.8 `feat(web-admin): phone layout for workers and the dashboard`.
- 3.9 `docs: admin parity checklist` (every web panel page and action listed and checked on the phone; README screenshots).
- → **Ask for push OK** → push → PR.

Each of 3.4–3.8 uses one shared helper for "full-screen dialog on small screens" and `MudTable Breakpoint`, and is
checked at 375×812 and at desktop width (the web panel must not change).

## Stage 4: Seller role → PR "Seller role"

Branch `feat/mobile-seller`.

- 4.1 `feat(workforce): seller role`
  `WorkerRole.Seller` (contracts + domain); policy `SalesAccess` (Admin or Seller) next to `AdminOnly`/`CourierOnly`.
- 4.2 `feat(api): sellers can place orders`
  Only what ordering needs moves from `AdminOnly` to `SalesAccess`: active products and categories (read), client search
  and details, create client, add shipping address, place order, assistant endpoints. Everything else stays `AdminOnly`.
  Integration test theory over the endpoint list: seller allowed → not 403, admin-only → 403.
- 4.3 `feat(ordering): record who placed an order`
  `orders.placed_by_worker_id` (from `ICurrentUser`, migration; seeded orders get the admin), `PlacedBy` in the order DTOs,
  `GET /api/orders/mine` (seller's own orders; other orders → 404 for a seller). Integration tests.
- 4.4 `feat(mobile): seller new order`
  Seller home = "Nowe zamówienie", reusing `NewOrder` + `AssistantPanel` from `Kamada.Web.Admin`, without admin-only
  actions (payments, ready-for-delivery, cancel).
- 4.5 `feat(mobile): seller's orders` (list "Moje zamówienia" + read-only details).
- 4.6 `feat(admin-panel): show who placed an order` (column + filter on the orders list).
- 4.7 `chore(seed): demo seller` (`sprzedawca1`) and `kierownik` gets Seller too (Admin + Seller + Courier).
- 4.8 `docs: seller role` (decisions log, README, Scalar descriptions).
- → **Ask for push OK** → push → PR.

## Stage 5: offline-ready ordering on the phone → PR "Phone app: orders with a poor connection"

The on-device assistant is only useful if an order can be **finished** without the server, so this stage comes first.
Branch `feat/mobile-offline-orders`.

- 5.1 `feat(web): connection quality monitor`
  `ConnectionMonitor` (Web.Shared): browser online/offline events + periodic `/health` ping (latency, consecutive
  failures) → `Good` / `Poor` / `Offline`, unit tested with `FakeTimeProvider`. Banner in the phone app.
- 5.2 `feat(api): idempotent creates for retried requests`
  Optional client-generated `requestId` on `PlaceOrderRequest`, `CreateClientRequest`, `AddressRequest`, unique per table;
  a repeated request returns the already created resource instead of a duplicate. Integration tests.
- 5.3 `feat(api): compact lookup data for the phone`
  `GET /api/products/lookup` (active products: id, SKU, name, price, stock, category) and `GET /api/clients/lookup`
  (active clients + shipping addresses), `SalesAccess`, `ETag`/`If-None-Match` so refreshes are cheap.
- 5.4 `feat(mobile): local store`
  Small IndexedDB module (JS interop) for the lookup data and an outbox; refreshed after login, role switch and when the
  connection becomes `Good`; **wiped on logout** (it holds client personal data).
- 5.5 `feat(mobile): queue orders while offline`
  When `Offline`/`Poor` the new-order form works on the local data (prices and stock marked "stan z HH:mm") and saves
  "Oczekujące zamówienie"; a sync loop sends entries in order (client → address → order) with their `requestId`s, keeps
  created ids in the entry, and marks conflicts (no stock, deactivated product) as "Wymaga uwagi" with edit / discard.
- 5.6 `docs(adr): offline ordering on the phone` (ADR 0009: what is stored on the device, idempotency, conflicts).
- → **Ask for push OK** → push → PR.

## Stage 6: on-device voice order assistant (fallback) → PR "Phone app: offline voice assistant"

Branch `feat/mobile-local-assistant`. The server assistant stays the default; the phone model is the fallback.

- 6.1 `docs: on-device assistant spike results`
  Measure on Dawid's phone (not guessed): LLM in the browser via WebGPU (WebLLM: Qwen2.5 1.5B / Gemma 2 2B /
  Llama 3.2 1B, 4-bit) and a CPU fallback (wllama/llama.cpp WASM, Qwen2.5 0.5B–1.5B GGUF); speech-to-text via Whisper in
  the browser (transformers.js, tiny/base/small, WebGPU or WASM). For each: download size, load time, time per dictation,
  accuracy on the existing `cases.json`, licence. Outcome: default model + a "lite" model, exact pinned versions.
  Only a results document is committed.
- 6.2 `refactor: shared order matching library`
  Pure matching code (`TextSimilarity`, client/address/product matchers, username suggestion, `OrderDraftResolver`
  thresholds, the extraction JSON schema and prompt) moves to `src/Shared/Kamada.Shared.Matching` (no EF, no ASP.NET),
  used by the server modules and by `Kamada.Web.Shared`. Architecture tests allow frontends to reference it.
  Server behaviour unchanged: unit tests and the assistant evals give the same results.
- 6.3 `feat(web): order assistant abstraction`
  `IOrderAssistant` in Web.Shared with `ServerOrderAssistant` (existing endpoints) and a `LocalOrderAssistant` stub;
  `NewOrder` talks only to the abstraction.
- 6.4 `feat(mobile): on-device language model` (JS interop module: load / unload, JSON-schema structured output,
  temperature 0, the same prompt as the server).
- 6.5 `feat(mobile): on-device speech recognition` (reuses `VoiceRecorder`'s 16 kHz mono PCM).
- 6.6 `feat(mobile): local order drafts`
  `LocalOrderAssistant` = speech-to-text → LLM mentions → `Kamada.Shared.Matching` over the local lookup data → the same
  `OrderDraft` DTO, so the form behaves exactly as with the server; accepting it goes to the outbox from stage 5.
- 6.7 `feat(mobile): assistant fallback policy`
  Server assistant when the connection is `Good` and the server reports it enabled; on-device when `Poor`/`Offline`,
  when the server answers 503, or when it times out. A chip shows "Asystent w telefonie"; a manual override exists.
  Unit tested.
- 6.8 `feat(mobile): offline assistant settings`
  Opt-in download (Wi-Fi recommended), progress, storage used (`navigator.storage.estimate`, `persist()`), delete;
  weights kept in Cache Storage and served from our own nginx (`/models`), not a third-party CDN at runtime; capability
  check (WebGPU, memory) hides the option on phones that cannot run it.
- 6.9 `test: on-device assistant evals` (dev-only page that runs `cases.json` through `LocalOrderAssistant` and reports
  accuracy and timing, driven by Playwright from the evals project; results table in the README next to the server models).
- 6.10 `docs(adr): on-device assistant as a fallback` (ADR 0010, decisions log, privacy note: on the phone nothing leaves
  the device).
- → **Ask for push OK** → push → PR.

## Decisions taken as defaults (change any of them before stage 1 starts)

| Question | Default |
|---|---|
| Phone app project name | Rename `Kamada.CourierApp` → `Kamada.MobileApp` (step 2.1) |
| Admin parity | Share the web admin pages through a Razor class library, made responsive |
| What a seller sees | Only orders they placed; no payments, no cancelling, no delivery planning |
| Role switching | Client-side only; the token carries all roles and the API checks roles per endpoint |
| Role change by an admin | Takes effect at the next token refresh (≤ 15 min) |
| Branching | One branch per stage from `main`, next stage after the merge |
| On-device models | Picked from measurements in step 6.1, not up front |
