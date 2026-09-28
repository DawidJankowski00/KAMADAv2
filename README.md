# KAMADA

Order management and courier delivery platform with **route optimization** – a .NET 10 modular monolith
with a Blazor WebAssembly admin panel and a Blazor PWA for couriers.

> 🚧 Work in progress. Full documentation (features, architecture, route optimization, benchmarks) will be
> added as the project evolves.

## Quick start

Requirements: Docker.

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Admin panel | http://localhost:5081 |
| API docs (Scalar) | http://localhost:5080/scalar |
| API health | http://localhost:5080/health |

Demo accounts (seeded in the Development environment):

| Role | Username | Password |
|---|---|---|
| Administrator | `admin` | `Admin123!` |
| Courier | `kurier1`, `kurier2`, `kurier3` | `Kurier123!` |

## Development

Requirements: .NET SDK 10.0.401+, Docker (PostgreSQL and integration tests).

```bash
docker compose up -d postgres
dotnet run --project src/Kamada.Api
dotnet run --project src/Web/Kamada.AdminPanel   # http://localhost:5081
dotnet test --solution Kamada.slnx
```

## Design decisions

See [docs/adr](docs/adr/README.md).
