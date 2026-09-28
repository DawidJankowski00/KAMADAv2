# 0006. Geocoding with Nominatim that never blocks saving an address

- **Status:** Accepted
- **Date:** 2026-09-28

## Context

Route optimization needs coordinates for every stop, but people type addresses. Geocoding services are
external: they can be slow, rate limited, down, or simply not find a new building. An admin entering a client
must not be blocked by that, and a wrong or missing pin must be fixable.

## Decision

- `IGeocodingService` in `Kamada.Shared.Infrastructure/Geocoding`, used by Customers (client addresses) and
  Delivery (route locations). Provider from configuration (`Geocoding:Provider`):
  - **Nominatim** (default, OpenStreetMap, no key) following its usage policy: an identifying `User-Agent`,
    at most one request per second (`RequestThrottle` inside the resilience pipeline, so retries are throttled
    too) and cached results (misses cached for 10 minutes).
  - **Google Geocoding API** – only when an API key is configured.
  - **None** – no lookups (tests use a deterministic fake).
- **Saving never fails because of geocoding.** When the lookup fails or finds nothing, the address is stored
  without coordinates and marked as such in the UI. The admin can drop a pin on the map (`Manual` source).
- An order whose address has no coordinates **cannot be optimized** – planning returns a clear error naming
  the orders, instead of guessing.
- Changing only the apartment number or address type keeps the pin; changing the building clears it and
  geocodes again. Addresses are archived, never deleted, because orders reference them.
- Delivery reads the **current** coordinates of an order's address (`ICustomerDirectory`), falling back to the
  snapshot stored in the order – a pin fixed after the order was placed is used by the next plan.

## Consequences

- Admin workflows keep working when OpenStreetMap is unavailable; the cost is a manual pin now and then.
- Demo data does not depend on geocoding at all: seeded addresses carry coordinates
  ([ADR 0007](0007-demo-data-through-real-use-cases.md)).
- The public Nominatim instance is fine for a demo, not for production volumes: a self-hosted instance or a
  commercial provider would be configured instead.
