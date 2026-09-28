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
| Courier app (PWA, best on a phone-sized window) | http://localhost:5082 |
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
dotnet run --project src/Web/Kamada.CourierApp   # http://localhost:5082
dotnet test --solution Kamada.slnx
```

## Route optimization

A delivery run starts at S (warehouse), visits n stops and ends at E. The order of stops that minimizes total
driving time is a fixed-endpoint travelling-salesman problem. KAMADA splits it into data and search
([ADR 0004](docs/adr/0004-travel-time-matrix-and-local-solver.md)):

1. **One travel-time matrix request:** (n+2)² durations and distances, which may be asymmetric. Providers:
   - **OSRM** (default, OpenStreetMap, no key).
   - **Google Routes API** (`computeRouteMatrix`, only when an API key is configured).
   - **Haversine straight-line estimate:** used by the tests and as an automatic fallback. Plans built on it are
     flagged as approximate.

   Matrices are cached by the set of points.
2. **Local solver** (in memory, chosen by stop count):

   | Stops | Solver | Result |
   |---|---|---|
   | n ≤ 8 | Brute force with branch-and-bound pruning | optimal |
   | n ≤ 15 | Held–Karp dynamic programming, O(n²·2ⁿ) | optimal |
   | n > 15 | Nearest neighbour + 2-opt + Or-opt, time-limited | heuristic, never worse than the input order |

3. **Google Maps links for the courier:** the order is handed over through official
   [Maps URLs](https://developers.google.com/maps/documentation/urls/get-started), split into chained parts when
   there are more than 9 waypoints. Nothing is ever read back from Google Maps: no scraping.

### Benchmarks

The benchmarks use random asymmetric matrices. They were run with BenchmarkDotNet 0.15.8 on .NET 10.0.12 and an
AMD Ryzen 7 7840HS. Mean time per solve:

| Stops | Brute force | Held–Karp | Heuristic |
|---:|---:|---:|---:|
| 6 | 8.95 μs | 2.17 μs | 1.71 μs |
| 8 | 321 μs | 11.0 μs | 1.33 μs |
| 10 | 16.6 ms | 111 μs | 3.47 μs |
| 12 | – | 1.43 ms | 7.37 μs |
| 15 | – | 15.2 ms | 7.84 μs |
| 25 | – | – | 30.1 μs |
| 50 | – | – | 121 μs |
| 100 | – | – | 555 μs |

Heuristic quality compared with the Held–Karp optimum (100 random matrices per size):

| Stops | Average gap | Worst gap | Optimal found | Gap of unoptimized input order |
|---:|---:|---:|---:|---:|
| 8 | 0.39 % | 8.59 % | 86 / 100 | 71.3 % |
| 10 | 0.53 % | 7.60 % | 80 / 100 | 93.5 % |
| 12 | 0.55 % | 6.27 % | 73 / 100 | 105.1 % |
| 15 | 0.85 % | 12.51 % | 68 / 100 | 127.4 % |

To reproduce:

```bash
dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --filter '*'
dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --quality
```

## Design decisions

See [docs/adr](docs/adr/README.md).
