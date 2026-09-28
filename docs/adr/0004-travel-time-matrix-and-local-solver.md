# 0004. Route optimization: travel-time matrix + local solver instead of testing permutations in Google Maps

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

A courier leaves a start point S (warehouse), visits n stops and finishes at E (often S again). We want the
order of stops with the lowest total driving time: a travelling-salesman problem with fixed endpoints.

The first idea was the one many people try first: build a Google Maps link for every permutation of the stops,
open it and read the travel time from the page, then keep the fastest one. We rejected it:

- **It breaks Google's Terms of Service.** Automated scraping of Google Maps is prohibited; the account/IP gets
  blocked and captchas appear.
- **It is brittle.** The page layout changes without notice, so the scraper breaks silently.
- **It does not scale.** n stops need n! route queries: 8 stops = 40 320, 10 stops = 3 628 800. Even at one query
  per second that is 11 hours for 8 stops.
- **It asks the wrong question.** Every full route shares the same legs; asking for whole routes repeats the
  same pairwise information over and over.

## Decision

Split the problem into **data** and **search**:

1. **One travel-time matrix request.** Ask a routing engine for durations and distances between every ordered
   pair of points: (n+2)² values in a single call (for 8 stops: 100 values, one request, instead of 40 320
   routes). The matrix may be asymmetric (one-way streets, turn restrictions), so every solver treats
   `A→B` and `B→A` separately.
   - `OsrmMatrixProvider` (default) – OSRM `/table`, open data (OpenStreetMap), no API key. Self-hosted
     container in docker compose, public demo server for development (≤ 1 request/s, throttled).
   - `GoogleRoutesMatrixProvider` (optional) – the official Routes API `computeRouteMatrix`, only when an API
     key is configured (user secrets / environment variable), requested in 25 × 25 tiles.
   - `HaversineMatrixProvider` – straight line × circuity factor / average speed. Used by tests and as the
     automatic **fallback** when the provider is down; such results are flagged `IsApproximate` and the UI
     says so. Planning never fails because an external service is unavailable.
   - Matrices are cached by the *set* of points (rounded to 5 decimals ≈ 1 m, sorted, de-duplicated), so
     "optimize" followed by "evaluate my manual order" costs one request. HTTP calls have timeouts and retries
     (`Microsoft.Extensions.Http.Resilience`).
2. **Solve locally, in memory.** `IRouteSolver.Solve(matrix, start, end)`, chosen by stop count
   (`RouteSolverSelector`, thresholds in configuration):
   - n ≤ 8 – `BruteForceSolver`: all n! orders with branch-and-bound pruning. Exactly the original idea, but on
     an in-memory matrix: microseconds instead of hours.
   - n ≤ 15 – `HeldKarpSolver`: dynamic programming over subsets, O(n²·2ⁿ). Optimal.
   - n > 15 – nearest neighbour + 2-opt + Or-opt local search with a time limit. Not optimal (flagged), never
     worse than the input order, typically within ~1 % of the optimum (see the benchmark project).
3. **Generate navigation links only at the end.** `GoogleMapsUrlBuilder` turns the final order into official
   Google Maps URLs (`https://www.google.com/maps/dir/?api=1&origin=…&destination=…&waypoints=…`) with
   coordinates and `%7C`-separated waypoints. That is a documented, key-less, ToS-compliant way to *hand a route
   to the courier's navigation app* – we never read anything back from Google Maps. Routes with more waypoints
   than one link allows (`GoogleMaps:MaxWaypointsPerLink`, default 9; the Maps URLs documentation allows 9 in the
   app / on desktop and only 3 in mobile browsers) are split into chained parts: the end of part k is the start
   of part k + 1 ("Część 1 z 3").

## Consequences

- Optimization costs one HTTP request plus milliseconds of CPU, regardless of how many orders exist.
- Solvers are pure functions over a matrix: unit-tested against hand-computed optima, property-tested
  (Held–Karp equals brute force on hundreds of random asymmetric matrices) and benchmarked with BenchmarkDotNet.
- Travel times from OSRM ignore live traffic; Google Routes can provide them when a key is configured.
  Estimated arrival times are therefore estimates.
- Straight-line fallback results can be noticeably worse routes; they are marked so the admin can re-run the
  optimization when the provider is back.
- The RouteOptimization module is stateless (no schema). Other modules use it through
  `IRoutePlanner` and `INavigationLinkBuilder` in its Contracts project.
