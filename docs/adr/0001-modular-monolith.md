# 0001. Modular monolith with two projects per module

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

The application has clearly separable business areas (catalog, customers, ordering, payments, workforce,
delivery, route optimization) but a single small team, a single database and a single deployment target.
Microservices would add network calls, distributed transactions and operational overhead without any
real benefit at this scale. A classic layered monolith, on the other hand, tends to turn into a tangle where
every feature reaches into every table.

## Decision

Build a **modular monolith**: one ASP.NET Core host (`Kamada.Api`) composed of independent modules.

Each module consists of exactly two projects:

- `Kamada.Modules.<Name>` – the implementation. Layers (`Domain`, `Application`, `Infrastructure`, `Api`)
  are folders/namespaces. Every type is `internal` except the static `<Name>Module` class that registers
  services and maps endpoints.
- `Kamada.Modules.<Name>.Contracts` – the public surface other modules may use (interfaces + DTOs).

Rules, enforced by `tests/Kamada.ArchitectureTests` (NetArchTest + project file inspection):

1. A module project may reference other modules **only through their `.Contracts` project**.
2. A `.Contracts` project never references a module implementation.
3. No type depends on another module's `Domain`/`Application`/`Infrastructure`/`Api` namespaces.
4. `Domain` does not depend on `Application`, `Infrastructure`, `Api`, EF Core, ASP.NET Core or Npgsql.
5. The implementation assembly exposes only the module entry point publicly.
6. Frontends reference only `Kamada.Shared.Contracts` and the shared UI library `Kamada.Web.Shared`.

## Alternatives considered

- **Four or five projects per module** (Domain/Application/Infrastructure/Api/Contracts): compiler-enforced
  layering, but 30+ projects for seven modules, slower builds and a lot of ceremony for little gain –
  `internal` visibility plus architecture tests give the same guarantees where they matter (between modules).
- **Microservices**: rejected – no independent scaling or team ownership needs; distributed consistency
  (e.g. order + stock reservation) would become much harder.

## Consequences

- Module boundaries are explicit and tested; a module could later be extracted into a service by replacing
  its in-process contracts with HTTP/messaging.
- Unit tests of module internals need `InternalsVisibleTo` (configured in each module project).
- Layering *inside* a module is enforced by tests, not by the compiler.
