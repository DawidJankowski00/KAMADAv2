# 0002. One database, one schema and one DbContext per module

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

Modules must not reach into each other's tables ([ADR 0001](0001-modular-monolith.md)), but the system is
deployed as one application with one PostgreSQL instance. Some business operations span modules and must
be atomic – most notably placing an order (Ordering) and decrementing stock (Catalog).

## Decision

- One PostgreSQL database; **each module owns a schema** (`workforce`, `catalog`, `customers`, `ordering`,
  `payments`, `delivery`).
- **Each module has its own `DbContext`** that maps only its own tables, has its own migrations
  (`Infrastructure/Persistence/Migrations`) and its own `__ef_migrations_history` table inside its schema.
  Registration goes through `AddModuleDbContext<T>(schema)` (snake_case naming, auditing interceptor,
  connection shared within the request scope).
- References between modules are plain ids – no foreign keys or navigation properties across schemas.
- Handlers use the module context through a small port (`IXxxDbContext` in `Application/Abstractions`)
  and EF Core LINQ directly – no generic repository on top of EF (which already is a unit of work).
- Cross-module atomic operations use `IUnitOfWork` (`Kamada.Shared.Infrastructure/Persistence`):
  every module context in a request scope is built on **one shared `NpgsqlConnection`**
  (`ScopedDbConnection`); `IUnitOfWork.ExecuteAsync` opens a transaction on it, enlists all module
  contexts (`Database.UseTransactionAsync`) and commits only when the work returns a successful `Result`.
  Each module still saves only its own context, through its public contract
  (e.g. Ordering → `ICatalogInventory.ReserveAsync`). This is a plain local transaction – no
  `TransactionScope`, no distributed transaction.
- Optimistic concurrency uses PostgreSQL `xmin` as a shadow property on every entity.
- On startup (dev, docker demo, tests) the host migrates all module contexts
  (`Database:ApplyMigrationsOnStartup`); in production migrations would be applied by the deployment pipeline.

## Alternatives considered

- **Single shared DbContext with all entities**: simplest, cross-module transactions for free, but nothing
  stops a module from querying another module's tables – boundaries erode quickly.
- **Database per module**: strongest isolation, but cross-module consistency needs sagas/outbox and the
  demo setup gets heavier. Not justified for a monolith.

## Consequences

- Schemas make ownership visible in the database itself (`\dn` in psql).
- Seven migration histories instead of one; `dotnet ef` commands need `-c <Context>` (see `CLAUDE.md`).
- A module can later move to its own database, but then the operations that use `IUnitOfWork` across it
  would need an outbox / saga instead of a local transaction.
- Concurrent writes to the same row (e.g. two orders taking the last unit of a product) are resolved by the
  `xmin` concurrency token: the losing request gets `409 Conflict` and can be retried.
