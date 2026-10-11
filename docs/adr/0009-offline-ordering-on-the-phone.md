# 0009. Offline ordering on the phone

- **Status:** Accepted
- **Date:** 2026-10-11

## Context

Sellers take orders on the phone app, often on a poor mobile connection. A request that hangs or fails must not lose an
order, and the seller must still be able to pick a client and products. The browser's `navigator.onLine` only says whether
there is a network, not whether the API is reachable.

## Decision

- **Connection quality.** `ConnectionMonitor` pings `GET /health/ping` (database check only, so frequent pings never wake
  Ollama or OSRM) and combines latency and consecutive failures with the browser's online/offline events into
  `Good` / `Poor` / `Offline`. "Online" is always confirmed by a ping.
- **What is stored on the device.** Only for a worker working as a seller (not admins, to keep personal data off devices):
  the active products (SKU, name, price, stock, categories) and active clients with their shipping addresses, plus the
  outbox of unsent orders. IndexedDB, one database per app, values as JSON. Lookup lists are refreshed after login, role
  switch and when the connection becomes good, with `ETag` / `If-None-Match` (the server answers `304`).
- **Privacy.** The copy holds clients' personal data. Signing out wipes the whole database (the app asks first when orders
  are waiting). An expired session wipes the lookup lists but keeps the outbox for the same worker; another worker signing
  in on the device starts from an empty database (`meta.owner`).
- **Idempotency.** `PlaceOrderRequest`, `CreateClientRequest` and `AddressRequest` take an optional client-generated
  `RequestId`, stored with a unique index. A repeated request returns what the first one created (also when identical
  retries race: the unique violation is caught and the winner is returned). An id used for something else is
  `409 Requests.IdReused`. This makes every step safe to repeat after a lost response.
- **Sending.** Entries go oldest first, in steps client → address → order; the ids the server returns are saved in the
  entry, so an interrupted send resumes where it stopped. Transport failures, 401, 429, 5xx and `Concurrency.Conflict`
  are transient: the run stops and tries again later. Any other refusal is a conflict that needs a human.
- **Conflicts.** The device's prices and stock may be stale, so the server decides at send time. A refused order (not
  enough stock, deactivated product, deleted client or address) becomes "Wymaga uwagi" with the server's message; the
  seller edits it (the entry keeps its request id and is sent again) or discards it. Nothing is changed silently.
- **Not in this stage.** The AI assistant needs the server and is hidden while the form works locally; an on-device
  assistant is stage 6. Totals shown offline are estimates; the server computes the real total from current prices.

## Consequences

- A seller can finish an order without any connection and nothing is lost or duplicated, at the cost of client data being
  on the phone while signed in.
- The same unique `request_id` columns are available to any other retrying client.
- Stock reserved at send time can differ from what the seller saw, which is why conflicts are surfaced, not hidden.
