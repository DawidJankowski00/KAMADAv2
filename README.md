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
| API health | http://localhost:5080/health |

## Development

Requirements: .NET SDK 10.0.401+, Docker (PostgreSQL and integration tests).

```bash
docker compose up -d postgres
dotnet run --project src/Kamada.Api
dotnet test --solution Kamada.slnx
```

## Design decisions

See [docs/adr](docs/adr/README.md).
