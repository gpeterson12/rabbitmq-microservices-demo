# AGENTS.md

## Repo status

Greenfield: `SPEC.md` is the authoritative blueprint and the only pre-existing file. Build from it exactly — structure, endpoints, event schemas, queue names, and seed data are all specified there. Do not invent extra infrastructure (DB, auth, tracing).

## Workflow

- Build in phases matching SPEC §3: (1) solution/project scaffolding,
  (2) Shared.Messaging, (3) OrderService, (4) InventoryService,
  (5) NotificationService, (6) docker-compose + Dockerfiles, (7) README.
  Commit after each phase with a message describing what was added.
- Run `dotnet build` after each phase and fix errors before moving on.
  Run `dotnet test` after test projects exist.
- If SPEC.md is ambiguous on a specific point, state the assumption
  made in the commit message rather than pausing to ask.

## Stack & layout

- .NET 10 minimal APIs (`Host.CreateApplicationBuilder`, no MVC controllers). Three services under `src/` (OrderService, InventoryService, NotificationService), shared library `shared/Shared.Messaging`, xUnit tests under `tests/` mirroring `src/`.
- RabbitMQ.Client **v7.x async API only**: `IChannel`, `CreateChannelAsync`, `BasicPublishAsync`, `BasicConsumeAsync`, `IAsyncBasicConsumer`/`AsyncEventingBasicConsumer`. Never `.Result`/`.Wait()` — most online examples show the old v6 sync API; do not copy them.
- Serilog console sink with structured logging; include hostname/container ID in log lines so competing consumers are distinguishable when scaled.
- README is a primary deliverable per SPEC §10, not an afterthought.

## Easy-to-get-wrong architecture rules

- Topology (full tables in SPEC §4): topic exchange `orders.topic` with keys `order.created` / `order.reserved` / `order.rejected`; fanout DLX `orders.dlx`; queue `inventory.order-created` declared with argument `x-dead-letter-exchange: orders.dlx`.
- Each service declares its exchanges/queues/bindings idempotently at startup (`durable: true`). No separate provisioning script — `docker compose up` must work from a clean slate.
- Initial RabbitMQ connection needs an in-code retry loop with exponential backoff; `depends_on: service_healthy` alone is insufficient.
- Event payload classes live in each service's own `Models/` folder — deliberately NOT in `Shared.Messaging`. Consumers bind by JSON schema convention, not shared compiled types. `Shared.Messaging` contains only `EventEnvelope` + `RabbitMqOptions`.
- OrderService publishes with publisher confirms enabled and awaits confirmation before returning `202 Accepted`; it never waits on downstream processing.
- Failure semantics: deserialization failure → `BasicNackAsync(requeue: false)` → dead-letter queue. Business rejection (unknown SKU / insufficient stock) is NOT a nack/retry — publish `order.rejected` and ack.
- Per-service Dockerfiles use multi-stage `sdk:10.0` → `aspnet:10.0` with **build context = repo root**, not the service folder, or the `Shared.Messaging` reference won't resolve.

## Running & verifying

- `docker compose up --build`: RabbitMQ on 5672, management UI http://localhost:15672 (guest/guest), services on host ports 5001–5003.
- Demo call: `curl -X POST localhost:5001/orders -H 'Content-Type: application/json' -d '{"sku":"SKU-WIDGET","quantity":1,"customerEmail":"a@b.com"}'`. Inventory seed: `SKU-WIDGET`=50, `SKU-GADGET`=10, `SKU-GIZMO`=0.
- `--scale inventory-service=3` conflicts with fixed host port mappings — omit fixed port mapping for inventory-service or document the tradeoff (SPEC §8).
- Acceptance checks (SPEC §14): full flow visible in `docker compose logs -f` and `GET /notifications` (:5003); a malformed JSON message published to `inventory.order-created` lands in `orders.dead-letter`; `dotnet test` passes.
- Config reaches services as env vars `RabbitMq__HostName` / `RabbitMq__UserName` / `RabbitMq__Password` (maps to `RabbitMqOptions`).

## Tests & explicit scope limits

- Unit tests only (InventoryService stock logic, OrderService request validation). No live-RabbitMQ integration tests for v1 — but keep publisher/consumer behind interfaces so they can be added later without a rewrite.
- Do not add: authentication, persistence/database, distributed tracing, Kubernetes manifests, saga/retry compensation on business rejection. CI is optional nice-to-have (`dotnet build && dotnet test` workflow).
