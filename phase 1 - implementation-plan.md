# Hospitality AI OS — Phase 1 Implementation Plan

**Governing principles:** [architecture-principles.md](architecture-principles.md) — this plan conforms to P1–P15; references like `(P5)` point there.
**Standards:** [solution-structure.md](solution-structure.md) · [coding-standards.md](coding-standards.md)
**Source spec:** [phase-1-product-spec-hospitality-ai-os.md](phase-1-product-spec-hospitality-ai-os.md)
**Language:** C# / .NET 10
**Target cloud:** Azure first, implemented purely as adapters (P5, P12).
**Approach:** Small, independently verifiable slices; each ends green and is committed before the next (P14). Foundation first, UI and AI last.

> **Before implementing or reviewing any slice, consult [architecture-principles.md](architecture-principles.md), [solution-structure.md](solution-structure.md), and [coding-standards.md](coding-standards.md).** They are the governing rules for this phase. Where this plan and those documents conflict, the documents win — fix the plan or record a waiver (§9).

---

## 1. Principles Conformance

How Phase 1 realizes each principle. Waivers, if any, are recorded in §9.

| Principle | Phase 1 realization |
|---|---|
| P1 AI-first, event-driven | Canonical envelope + all spec §8 events (C3); outbox → transport → handlers (C4–C6); controllers subscribe to events (H, I). |
| P2 Humans as physical executors | `HumanAction` aggregate + state machine (B9); router (I3); continuation handlers resume without managers (I4); staff surface (J2). |
| P3 Canonical domain ownership | Typed platform IDs (B1); no vendor fields in domain (B, arch tests A2); `ExternalReference` (E2). |
| P4 Grounded, honest AI | Read capabilities return source ID + version (D3); grounding + "no authoritative source" path (G5); evals for unknown/policy cases (H3). |
| P5 Everything behind an interface | Port catalogue §2; binding mechanism (A3); local adapters now, Azure adapters in Milestone K. |
| P6 Inference as tasks | Inference ports + `IInferenceRouter` with task bindings/fallback (G1); Fake + Anthropic adapters (G2); prompt registry (G3). |
| P7 Capabilities only | Capability registry + executor (D1–D2); allow-listed tool loop (G4). |
| P8 Idempotency | Inbox dedupe (C6); capability idempotency keys (D2); HumanAction double-complete no-op (B9); idempotent deploys (K0). |
| P9 Dependency direction | Solution layout §4; enforced by architecture tests (A2). |
| P10 Tenant isolation & security | Tenant query filters (C2); auth roles (§3); Key Vault + managed identity (K3); append-only audit (C7). |
| P11 Observable & traceable | Correlation/causation (C7); AITrace (G6); OpenTelemetry + metrics (L1). |
| P12 IaC, idempotent deploys | Terraform + GitHub Actions per §5; delivery foundation K0. |
| P13 Testable by design | Port contract test suites (C1, C4, G1…); architecture tests (A2); Fake model evals (H3). |
| P14 Small green increments | Milestones/slices §6; progress §10. |
| P15 DI + single composition root | Built-in `Microsoft.Extensions.DependencyInjection`; constructor injection of ports; one `Add<Module>()` per project; `Api` is the only composition root and validates the container in every environment; architecture tests ban service location and `IConfiguration` in business code (A5). |

---

## 2. Port Catalogue (Phase 1)

Concrete ports required by Phase 1 under P5/P6.

### 2.1 Infrastructure Ports

| Port | Purpose | Local/dev adapter | Azure adapter | Other options (later) |
|---|---|---|---|---|
| `IUnitOfWork` (Ports), `I{Aggregate}Repository` (in each domain project) | Transactional persistence of aggregates | In-memory (pure, no EF); EF Core + SQLite | EF Core + Azure SQL / Azure Database for PostgreSQL | Cosmos DB, AWS RDS |
| `IReadStore` / query services | Read-side queries for APIs & capabilities | In-memory; EF Core + SQLite | Same as above | Read replicas, Cosmos |
| `IOutboxStore`, `IInboxStore` | Transactional outbox / processed-event tracking | In-memory; EF Core + SQLite | EF Core | — |
| `IEventTransport` | Deliver events between dispatcher and handlers | In-memory channel | Azure Service Bus | Event Grid, Kafka, RabbitMQ, AWS SNS/SQS |
| `IEventStore` | Durable event log for replay/audit | In-memory; EF Core + SQLite | EF Core / Event Hubs capture | EventStoreDB |
| `IBlobStore` | Evidence photos, content assets | Local filesystem | Azure Blob Storage | S3, GCS |
| `ISecretStore` | Secrets and API keys | User-secrets / env vars | Azure Key Vault | AWS Secrets Manager, Vault |
| `IDistributedCache` (platform-owned wrapper) | Cache for property context | In-memory | Azure Cache for Redis | Any Redis |
| `IDistributedLock` | Single-flight for critical sections | In-process lock | Blob lease / Redis | — |
| `ISearchIndex` | Keyword search over content | In-memory | Azure AI Search | Elasticsearch, OpenSearch |
| `IVectorStore` | Semantic retrieval for content | In-memory cosine | Azure AI Search vectors | pgvector, Qdrant |
| `IIdentityProvider` / `ITokenValidator` | Staff/admin auth, guest session tokens | Dev JWT issuer | Microsoft Entra ID / Entra External ID | Auth0, Cognito |
| `ICurrentActor` | Who is acting (user, agent, system) | From claims | From claims | — |
| `IRealtimeNotifier` | Push HumanAction updates to staff UI | In-process SignalR | Azure SignalR Service / Web PubSub | Pusher, Ably |
| `IScheduler` | Delayed work (SLA expiry, retries) | In-process timer | Service Bus scheduled messages / Durable Functions | Hangfire, Quartz |
| `IFeatureFlags` | Toggle behaviors per property | Config file | Azure App Configuration | LaunchDarkly |
| `ITelemetry` (via OpenTelemetry API) | Logs, metrics, traces | Console exporter | Azure Monitor / App Insights exporter | Any OTLP backend |
| `IIdGenerator` | Canonical IDs | GUID v7 | GUID v7 | — |
| `TimeProvider` (BCL abstraction) | Clock | System | System | Fake in tests |

### 2.2 Inference Ports

| Port | Used for |
|---|---|
| `IChatModel` | Multi-turn chat with tool calls (Concierge, Guest Service) |
| `IStructuredExtractor` | Schema-constrained extraction (service type, item, quantity, location) |
| `IClassifier` | Intent routing (question vs. service request vs. status vs. maintenance) |
| `IEmbeddingModel` | Embeddings for content retrieval |
| `IReranker` | Optional reranking of retrieved context |
| `IEvaluationJudge` | LLM-as-judge in AI evals |
| `IInferenceRouter` | Resolves a task name → provider + model + parameters, with fallback chain |
| `IPromptRegistry` | Versioned prompts, independent of provider |
| `IAiTraceSink` | Records every inference call (model, provider, prompt version, tokens, latency) |

Example binding configuration:

```json
"Inference": {
  "Providers": {
    "anthropic":   { "Adapter": "Anthropic",   "SecretRef": "anthropic-api-key" },
    "azureopenai": { "Adapter": "AzureOpenAI", "Endpoint": "https://...", "SecretRef": "aoai-key" },
    "fake":        { "Adapter": "Fake" }
  },
  "Tasks": {
    "concierge.answer":      { "Provider": "anthropic",   "Model": "claude-sonnet-5-5", "Fallback": ["azureopenai:gpt-x"] },
    "guestservice.extract":  { "Provider": "anthropic",   "Model": "claude-haiku-5-5" },
    "intent.classify":       { "Provider": "anthropic",   "Model": "claude-haiku-5-5" },
    "content.embed":         { "Provider": "azureopenai", "Model": "text-embedding-x" },
    "eval.judge":            { "Provider": "anthropic",   "Model": "claude-opus-5-5" }
  }
}
```

Bindings can be overridden per property/tenant. Initial adapters: `Fake` (deterministic, tests & evals) and `Anthropic`; later `AzureOpenAI` / Azure AI Foundry, local (`Ollama`). Adapters may internally use `Microsoft.Extensions.AI` or vendor SDKs; those types never cross the port.

### 2.3 Integration Ports (spec §10)

`IPmsConnector`, `ICmsConnector`, `IPosConnector`, `IMessagingConnector`, `ICrsConnector` (placeholder), `IPaymentConnector` (placeholder). Resolved per property. Initial: `MockPms`, `NativeCms`, `MockPos`, `MockMessaging`.

### 2.4 Binding Mechanism

One pattern for all three port families:

- `IAdapterFactory<TPort>` — each adapter project registers a named factory (`"AzureServiceBus"`, `"InMemory"`, …).
- `IBindingResolver<TPort>` — resolves `(port, operation?, tenant/property?)` → adapter instance from configuration.
- Adapters register via an extension method in their own project (`services.AddAzureAdapters()`); the host calls only the ones it wants.
- Startup validation: every required port has a binding; unknown adapter names fail fast.
- Health checks per bound adapter.

Implemented in A3: abstractions in `HospitalityOS.Ports` (`Bindings/`), implementation in `HospitalityOS.Bindings`. Configuration shape:

```json
"Bindings": {
  "EventTransport": {
    "Default":    { "Adapter": "InMemory" },
    "Operations": { "guest-notifications": { "Adapter": "AzureServiceBus", "Settings": { "...": "..." } } },
    "Tenants":    { "tenant-a": { "Default": { "Adapter": "..." }, "Operations": { "...": { "Adapter": "..." } } } }
  }
}
```

- Host: `services.AddBindings(configuration)`; per port: `services.AddPortBinding<TPort>()` (key defaults to the type name without `I`).
- Adapter projects: `services.AddSingleton<IAdapterFactory<TPort>, MyFactory>()`; factories may override `ValidateSettings`.
- Consumers: `IBindingResolver<TPort>.Resolve(new BindingContext(operation, tenant))`; precedence tenant+operation → tenant default → operation → default; one cached instance per binding.
- Startup fails (`OptionsValidationException`) on: missing default, missing/unknown adapter name, invalid settings, or bindings for an unregistered port.
- Health check `bindings` resolves and probes (`IAdapterHealthProbe`) every configured binding.
- Configuration is read once; changes require restart.

---

## 3. Phase 1 Technical Decisions

| Area | Decision |
|---|---|
| Shape | Modular monolith: one API host, many class-library modules (P9 allows later split). |
| Events | Transactional outbox → dispatcher → `IEventTransport` → handlers with inbox idempotency. At-least-once, retry, dead-letter, replay. |
| Frontends | React + TypeScript + Vite (guest-web, staff-web, admin-web); typed client generated from OpenAPI. |
| Auth roles | Admin, Manager, Worker; guest via signed stay/session token. |
| Tests | xUnit v3 on Microsoft.Testing.Platform with built-in `Assert` (FluentAssertions 8+ requires a commercial license — not used); NetArchTest; WebApplicationFactory; shared port contract suites. |
| IDs | Strongly typed IDs (`PropertyId`, `HumanActionId`…) over GUID v7. |
| Time | `TimeProvider` everywhere; property time zone on `Property`. |
| IaC | Terraform; app on Azure Container Apps; pipelines in GitHub Actions (§5). |

---

## 4. Solution Layout

Phase 1 projects, laid out per [solution-structure.md](solution-structure.md) (layering, naming, project internals, build configuration).

```text
HospitalityOS.slnx
src/
  Core/
    HospitalityOS.SharedKernel/            # IDs, Result, Entity/AggregateRoot, errors
    HospitalityOS.Ports/                   # infrastructure ports (2.1) + binding abstractions (2.4)
    HospitalityOS.Ports.Inference/         # inference ports (2.2)
    HospitalityOS.IntegrationContracts/    # hospitality integration ports (2.3)
  Domains/
    HospitalityOS.Domain.Organizations/
    HospitalityOS.Domain.Properties/       # Property, Space, Accommodation*, Amenity, Hours, Policy, Content
    HospitalityOS.Domain.Dining/
    HospitalityOS.Domain.Activities/       # PropertyEvent (named to avoid clash with domain events)
    HospitalityOS.Domain.Services/         # Service, ServiceRequest
    HospitalityOS.Domain.Operations/       # HumanAction, TaskAssignment
    HospitalityOS.Domain.Guests/           # Guest, StayContext, Conversation, GuestMessage
  Platform/
    HospitalityOS.Application/             # commands/queries per module, uses ports only
    HospitalityOS.Bindings/                # binding resolver, startup validation, health checks (2.4)
    HospitalityOS.Eventing/                # envelope, outbox dispatcher, inbox, retry, replay
    HospitalityOS.Capabilities/            # registry, executor, definitions
    HospitalityOS.Ai/                      # gateway, inference router, context builder, traces
    HospitalityOS.Workflows/               # Concierge, GuestService, HumanAction Router
    HospitalityOS.Authorization/
    HospitalityOS.IntegrationRuntime/      # provider registry/resolver, ExternalReference
  Adapters/
    HospitalityOS.Adapters.Local/          # in-memory/filesystem/dev implementations of non-persistence ports
    HospitalityOS.Adapters.InMemory/       # pure in-memory persistence: repositories, unit of work, read store, outbox/inbox, event store
    HospitalityOS.Adapters.EfCore/         # repositories, outbox/inbox, read store (provider-neutral EF)
    HospitalityOS.Adapters.EfCore.Sqlite/
    HospitalityOS.Adapters.EfCore.SqlServer/       # Azure SQL (later)
    HospitalityOS.Adapters.Azure/                  # Service Bus, Blob, Key Vault, AI Search, SignalR, App Config (later)
    HospitalityOS.Adapters.Inference.Fake/
    HospitalityOS.Adapters.Inference.Anthropic/
    HospitalityOS.Adapters.Inference.AzureOpenAI/  # later
    HospitalityOS.Integrations.Mock/       # MockPms, MockPos, MockMessaging
    HospitalityOS.Integrations.Native/     # NativeCms
  Hosts/
    HospitalityOS.Api/                     # composition root: picks adapters via config
  Contracts/
    HospitalityOS.Contracts/               # API DTOs, event payloads, capability schemas
apps/
  guest-web/  staff-web/  admin-web/
tests/
  HospitalityOS.ArchitectureTests/
  HospitalityOS.PortContractTests/
  HospitalityOS.UnitTests/
  HospitalityOS.IntegrationTests/
  HospitalityOS.EndToEndTests/
  HospitalityOS.AiEvals/
.github/
  workflows/                               # CI, IaC plan/apply, deploy, drift detection (§5)
  actions/                                 # reusable composite actions
deploy/
  terraform/
    modules/                               # one module per infrastructure port
      azure/                               # Azure implementations
      <other-cloud>/                       # later, if a binding moves to another cloud
    environments/
      dev/  staging/  prod/                # compose modules; tfvars select provider per binding
  migrations/                              # generated idempotent DB migration bundles (build output)
dev/
  seed-data/                               # Aurora Grand reference property
```

Dependency direction follows P9 and [solution-structure.md §2](solution-structure.md); enforced by architecture tests (A2).

---

## 5. Phase 1 Delivery Setup (implements P12)

### 5.1 Pipelines (`.github/workflows/`)

| Workflow | Trigger | Does |
|---|---|---|
| `ci.yml` | PR, push | Restore, build, unit + architecture + contract + integration tests, frontend lint/build, AI evals against Fake provider. |
| `iac-plan.yml` | PR touching `deploy/` | `terraform fmt -check`, `validate`, `tflint`, security scan (checkov/tfsec), `plan` per environment; plan posted as PR comment. |
| `build-artifacts.yml` | Merge to `main` | Build & push SHA-tagged images, produce migration bundle, SBOM, sign artifacts. |
| `deploy.yml` (reusable) | Called per environment | 1) `terraform apply` of reviewed plan, 2) idempotent DB migration bundle, 3) deploy image SHA (new Container Apps revision), 4) smoke tests + health checks, 5) shift traffic; on failure keep/restore previous revision. |
| `promote.yml` | Manual / on success | `deploy.yml` for staging then prod, gated by GitHub environment protection rules. |
| `drift-detection.yml` | Scheduled (daily) | `terraform plan` per environment; non-empty plan opens an issue/alert. |
| `ai-evals-live.yml` | Manual / nightly | AI evals against real bound models in non-prod for model comparison. |

Pipeline rules: concurrency group per environment; deploy metadata records git SHA, image digest, plan, migration version; rollback = redeploy previous SHA; `prevent_destroy` on databases, storage, key vaults, and destructive changes require explicit approval.

### 5.2 Environments

| Env | Purpose | Deploy |
|---|---|---|
| `local` | Developer machine; local adapters (SQLite, in-memory) | none |
| `dev` | Shared integration, Azure adapters | auto on merge to `main` |
| `staging` | Prod-like, demo & live AI evals | promote, optional approval |
| `prod` | Customers | promote, required approval |

---

## 6. Milestones and Slices

Each slice: **Goal → Deliverables → Done when**. Sized for one focused session.

### Milestone A — Foundation (Epic 1)

**A1. Repository + solution skeleton**
- `.gitignore`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` per [solution-structure.md §5](solution-structure.md).
- `.editorconfig` encoding [coding-standards.md](coding-standards.md) naming rules (PascalCase types, camelCase fields with no prefix, etc.) at error severity.
- Projects from §4 that Milestones A–C need, with references per P9; `Api` exposes `/health`.
- Done when: `dotnet build` and `dotnet test` pass from root; `dotnet format --verify-no-changes` passes; a field named `_foo` or a class named `foo` fails the build.

**A2. Architecture tests**
- NetArchTest rules for P5, P9, and [solution-structure.md §9](solution-structure.md) (layering, no vendor SDK outside adapters, adapter classes `internal sealed`).
- Done when: tests pass, and a deliberately bad reference fails them.

**A3. Binding mechanism**
- `IAdapterFactory<TPort>`, `IBindingResolver<TPort>`, config schema, startup validation, per-operation and per-tenant overrides, health checks per binding.
- Done when: test binds a sample port to two different adapters for two operations from config; missing/unknown binding fails at startup.

**A4. CI**
- `.github/workflows/ci.yml`: restore, build, test; concurrency group; caching.
- Done when: workflow is valid (actionlint) and passes on push.

**A5. Dependency injection conformance (P15)**
- Host builds the container with `ValidateOnBuild` + `ValidateScopes` in every environment.
- Architecture tests: no `IServiceProvider`/`IServiceScopeFactory`/`IConfiguration` constructor parameters, fields, or properties outside hosts, the binding mechanism, and `DependencyInjection` classes; `IServiceCollection` extensions only in a project's single public static `DependencyInjection` class.
- Done when: tests pass, a deliberate violation fails them, and CI is green.

### Milestone B — Canonical Domain Core (Epic 2)

**B1. SharedKernel** — typed IDs, `Entity`/`AggregateRoot` with raised domain events, `Result`/`Error`, validation errors, `TenantId`; `IIdGenerator` port with GUID v7 adapter in `Adapters.Local`.
**B2. Organization + Property** — invariants, `PropertyCreated`/`PropertyUpdated`, validation errors (Journey 1).
**B3. Spaces + Accommodation** — hierarchical `Space`; `AccommodationType`, `AccommodationUnit`.
**B4. OperatingHours** — multiple intervals/day, closed days, temporary closures, date overrides, overnight intervals, time zone, versioned (Journeys 2, 16).
**B5. Amenity, Service, Policy, ContentItem** — policy separate from content (Journey 4).
**B6. Dining** — `Restaurant`, `Menu`, `MenuItem` with dietary attributes, availability; no POS fields.
**B7. PropertyEvent (activities)** — local-time ranges, category, audience, upcoming-only query.
**B8. Guest context** — `Guest`, `StayContext`, `Conversation`, `GuestMessage`.
**B9. ServiceRequest + HumanAction** — full spec §7 fields and an explicit state machine; illegal transitions rejected; double-complete is a no-op. `TaskAssignment`.

### Milestone C — Persistence + Event Backbone (Epic 3)

Persistence has two interchangeable implementations behind the same ports, selected by binding: **InMemory** (`HospitalityOS.Adapters.InMemory`, pure collections, no EF — not the EF Core InMemory provider) and **EF Core + SQLite**. Both pass the same contract suite; Azure SQL (K1) passes it too.

**C1. Persistence ports + in-memory adapter + contract tests** — `IUnitOfWork`, repositories, `IReadStore`; reusable contract test suite; `HospitalityOS.Adapters.InMemory` passing it (atomic commit of aggregates + raised events, tenant filtering, optimistic concurrency). A2 allow-list updated so this persistence adapter may reference `Domains`.
**C2. EF Core adapter (SQLite)** — DbContext, configurations, tenant query filters, migrations; passes the same C1 contract tests; tenant isolation test runs against both adapters.
**C3. Event envelope + contracts** — spec §8 envelope, versioned payloads for all §8 events, serializer.
**C4. Eventing ports + local adapters** — `IEventTransport`, `IOutboxStore`, `IInboxStore`, `IEventStore`; in-memory transport; in-memory and EF Core implementations of outbox/inbox/event store; contract tests run against both.
**C5. Outbox dispatcher** — events saved in same transaction as aggregate; dispatch, retry/backoff, dead-letter.
**C6. Idempotent handlers + replay** — inbox dedupe per (handler, eventId); replay by range/correlation; Journey 19 test.
**C7. Correlation/causation + audit** — ambient execution context; append-only `AuditRecord`.

### Milestone D — Capability Registry (Epic 4)

**D1. Definitions + registry** — all spec §11 fields, JSON Schema in/out, `/api/v1/capabilities`.
**D2. Executor** — lookup → validate → authorize → confirmation policy → idempotency → execute → validate output → invocation record + events → audit.
**D3. Read capabilities** — Property/Dining/Events queries returning source record ID + version for grounding.

### Milestone E — Integration Abstraction (Epic 5)

**E1. Contracts** — six connector interfaces, canonical types only.
**E2. Runtime** — `IntegrationProvider`, `IntegrationConfiguration`, per-property resolver (on A3), `ExternalReference`, health.
**E3. Connectors** — MockPms, NativeCms, MockPos, MockMessaging; Journey 18 provider-swap test.

### Milestone F — Property APIs + Seed Data (Epics 2/6)

**F1.** Organization/Property APIs (validation, tenant isolation, audit).
**F2.** Spaces, Amenities, Hours, Policies, Content APIs.
**F3.** Restaurants/Menus, Activities, Services APIs.
**F4.** Aurora Grand seed data (pool, gym child policy, vegetarian items, tonight's events, room 412).

### Milestone G — AI Gateway + Context Engine (Epic 7)

**G1. Inference ports + router** — §2.2 ports, `IInferenceRouter` with task bindings, fallback chains, per-tenant overrides; contract tests.
**G2. Fake + Anthropic adapters** — deterministic scripted fake; Anthropic adapter passing contract tests.
**G3. Prompt registry** — versioned prompt files; version recorded per call.
**G4. Tool loop** — allow-listed capabilities exposed as tools; structured output validation; nothing else callable.
**G5. Context builder + grounding** — structured sources first, then `ISearchIndex`/`IVectorStore`; citations; "no authoritative source" path.
**G6. AITrace** — spec §26 fields, redaction, `/api/v1/ai/traces`.
- Done when: "What time does the pool close tonight?" works end-to-end; trace names the OperatingHours record + version; switching `concierge.answer` between Fake and Anthropic is config-only.

### Milestone H — AI Concierge (Epic 8)

**H1.** Conversations API + Concierge Controller (`GuestMessageReceived` → `GuestQuestionAnswered`).
**H2.** Policy, dining, events, unknown-question handling (Journeys 6, 7, 8, 17).
**H3.** AI eval harness (uses `IEvaluationJudge`); scenarios 1–5, 11, 12; runnable against any bound model.

### Milestone I — Guest Service Workflow (Epics 9, 11)

**I1.** GuestService capabilities + extraction via `IStructuredExtractor`.
**I2.** HumanAction capabilities + events per transition.
**I3.** HumanAction Router (deterministic rules, reassignment).
**I4.** Continuation handlers (completed → complete request → notify; failed → exception).
**I5.** Messaging via `IMessagingConnector` → `GuestNotificationSent`.
- Done when: towel, pillow, AC, status, duplicate-completion, failed-action journeys pass with one correlationId; evals 6–10 pass.

### Milestone J — Frontends (Epics 6, 8, 10)

**J1.** Frontend workspace, OpenAPI client, dev auth.
**J2.** Staff web (4 views, Accept/Start/Complete/Fail, live updates via `IRealtimeNotifier`).
**J3.** Guest web (chat + status).
**J4.** Admin web (spec §34 sections, AI trace viewer).

### Milestone K — Azure Adapters + Infrastructure as Code

Each adapter passes the same contract tests as its local counterpart (P13) and ships with its Terraform module (P12).

**K0. IaC + delivery foundation** (can be pulled earlier for a cloud dev env sooner)
- Bootstrap: idempotent script/workflow for Terraform state storage + GitHub OIDC federated identity.
- Terraform skeleton: resource group, container registry, Container Apps environment, Key Vault, Log Analytics, managed identity.
- Workflows: `iac-plan.yml`, `build-artifacts.yml`, reusable `deploy.yml`, `drift-detection.yml`.
- Done when: `/health` deploys to `dev` on merge to `main`; re-running on the same SHA is a no-op; drift detection reports clean.

**K1.** EF Core SQL Server adapter (Azure SQL) — or PostgreSQL, per choice.
**K2.** Azure Service Bus `IEventTransport` + scheduled messages `IScheduler`.
**K3.** Blob Storage, Key Vault, App Configuration adapters.
**K4.** Azure AI Search (`ISearchIndex`, `IVectorStore`), Azure SignalR (`IRealtimeNotifier`), Entra ID (`IIdentityProvider`).
**K5.** Azure OpenAI / Foundry inference adapter.
**K6.** Azure Monitor exporter; staging + prod environments, `promote.yml`, `ai-evals-live.yml`; idempotent migration + seed steps in `deploy.yml`.

### Milestone L — Hardening (Epic 12)

**L1.** Observability: event/capability metrics, failed-event view.
**L2.** End-to-end tests automating demo script spec §43 (against local and Azure bindings).
**L3.** Definition-of-Done audit against spec §42 and principles conformance (§1).

---

## 7. Working Agreement

Follows P12–P14. Phase 1 specifics:
- One slice at a time; mark ✅ in §10 when green and committed.
- Every infra change goes through PR → `iac-plan` → reviewed apply.

## 8. Decision Log

| Date | Decision |
|---|---|
| 2026-10-23 | C#/.NET 10 modular monolith; Azure first target via adapters only; local adapters (SQLite, in-memory) for dev/tests; Fake + Anthropic inference adapters first. |
| 2026-10-23 | Terraform (not Bicep) + GitHub Actions for idempotent IaC deployments. |
| 2026-10-23 | Architecture principles extracted to [architecture-principles.md](architecture-principles.md); this plan references them by ID. |
| 2026-10-23 | A3: binding implementation lives in new platform project `HospitalityOS.Bindings`; `Ports` may reference vendor-neutral `Microsoft.Extensions.*.Abstractions`. Startup validation uses `ValidateOnStart`. `.editorconfig` requires accessibility modifiers except on interface members. LF line endings enforced via `.gitattributes`. |
| 2026-10-23 | AI-assisted onboarding from website/documents is out of Phase 1 scope; specified as Phase 2 candidate 2.1 in [phase-2-product-spec-hospitality-ai-os.md](phase-2-product-spec-hospitality-ai-os.md). Phase 1 keeps manual admin configuration. |
| 2026-10-23 | A1: `.slnx` solution format; xUnit v3 on Microsoft.Testing.Platform; built-in asserts (FluentAssertions licensing); aggregate repository interfaces live in domain projects so `Ports` stays SharedKernel-only. |
| 2026-10-23 | Solution structure rules and coding standards split into [solution-structure.md](solution-structure.md) and [coding-standards.md](coding-standards.md). |
| 2026-10-24 | Persistence has two independent implementations behind the same ports: pure in-memory (`HospitalityOS.Adapters.InMemory`, no EF; EF Core InMemory provider rejected as unrepresentative) and EF Core + SQLite. Both must pass one shared contract suite (C1, C2, C4). In-memory lives in its own project because repository adapters reference `Domains`, which non-persistence local adapters must not. |
| 2026-10-24 | B1 also adds `IIdGenerator` (Ports) with a GUID v7 implementation in `Adapters.Local`, since domain code may not call `Guid.NewGuid()`. |
| 2026-10-24 | B1: `Result` is a class (simpler with nullable reference types; allocation irrelevant at this scale). Strongly typed IDs are hand-written `readonly record struct`s implementing `ITypedId` that reject `Guid.Empty`; a source generator can come later. `Error` keeps its name with a scoped CA1716 suppression (clashes only with a VB keyword). Domain events carry `OccurredAt` only; event ID, correlation and causation belong to the C3 envelope. |
| 2026-10-24 | B2: IDs referenced across domains (`OrganizationId`, `PropertyId`, alongside `TenantId`) live in `SharedKernel/Ids`, since domain projects may not reference each other; aggregate-local IDs stay in their domain. Tenant and organization are distinct IDs, as in the spec §8 envelope; an organization belongs to one tenant. Organization also raises `OrganizationCreated`/`OrganizationUpdated` (P1), beyond spec §8's minimum. Property time zones must be IANA IDs (`TimeZoneInfo.HasIanaId`) so they mean the same on every OS. Updates that change nothing are successful no-ops raising no event (P8). Repository interfaces start minimal (`GetByIdAsync`, `AddAsync`); C1 refines them with the unit of work. |
| 2026-10-24 | CA1716 (identifiers matching VB keywords: `Property`, `Error`, `Event`) disabled in `.editorconfig`: C#-only codebase, and the spec's canonical names use these words. |
| 2026-10-24 | B3: `SpaceId` and `AccommodationUnitId` join SharedKernel/Ids (referenced by dining, operations, guests); `AccommodationTypeId` stays in Domain.Properties. A space's parent is fixed at creation and must already exist in the same property, so cycles are impossible; re-parenting deferred. Any `SpaceKind` may contain any other. Factories that cross aggregates (`AccommodationUnit.Create`, `Space.Create`) take the loaded related aggregates to check same-property/tenant, but store only their IDs. Unit-number uniqueness per property is an application-layer check (`FindByUnitNumberAsync`) backed by a unique index in C2. Space and accommodation events (`SpaceCreated`, …) go beyond spec §8's minimum, per P1. |
| 2026-10-24 | B4: `OperatingHours` lives in Domain.Properties and references its subject by kind + raw ID (`HoursSubject`), so Dining's restaurants can use it without a cross-domain reference; one record per subject (application check + unique index in C2). Schedules store property-local times only; the property's time zone is passed to `GetStatusAt`, not copied, so a time-zone change cannot leave hours stale. Precedence: temporary closure > date override > weekly. An interval with close <= open ends next day; 00:00-00:00 means all day. Overnight hours from the previous day still apply on a closure/override day. Overlaps between an override's overnight interval and the following day are not validated in Phase 1. `AggregateRoot.Version` starts at 0, +1 per raised event (so 1 after creation) and is cited by grounded answers (D3/G5) and used for optimistic concurrency (C1). `ValueList<T>` gives records value equality over lists. |
| 2026-10-24 | B5: `Amenity`, `Policy`, `ContentItem` in Domain.Properties; `Service` in Domain.Services (beside B9's `ServiceRequest`). `HoursSubject` generalized to `SubjectReference` (kind + raw ID) so policies and content can say what they are about; subject existence/same-property is an application-layer check. `TextRules` moved to `SharedKernel/Validation` so every domain validates text identically; it gains `Code` for lower-case machine codes (`housekeeping.amenities`, `bath_towel`). Policies are created as drafts and must be activated; retired policies are immutable and cannot be reactivated (new policy instead), so traces citing an old version stay meaningful. Content can be archived and republished. Status changes raise the aggregate's `*Updated` event (spec §8 lists only Created/Updated). Service codes are unique per property (application check + unique index in C2); items are requested by code with a per-request quantity cap that I1 extraction validates against. |
| 2026-10-24 | B6: Menu items live inside the `Menu` aggregate (edited as a unit; one `MenuUpdated` per change, as spec §8 lists only `MenuUpdated`), with caller-generated `MenuItemId`s kept stable across edits so answers can cite an item. Prices are optional (`null` = not published, so the AI says it doesn't know) and one currency per menu. Dietary tags are normalized (sorted, de-duplicated); `Vegan` implies `Vegetarian`; `NutFree` + `ContainsNuts` is rejected; an absent tag means "not stated", never "safe". A restaurant's location is a bare `SpaceId` because Dining may not reference Domain.Properties; the application layer checks it. `Money` lives in `SharedKernel/Pricing` (namespace avoids clashing with the type). `DietaryAttribute` renamed `DietaryTag` (CA1711 reserves the `Attribute` suffix). |
| 2026-10-24 | P15 added: DI via the built-in `Microsoft.Extensions.DependencyInjection` container. Unity was requested and rejected: `Unity.Microsoft.DependencyInjection` was last released in Dec 2019 (targets .NET Core 3.1) and Unity.Container 6 never left RC, so it is unmaintained for .NET 10. A maintained third-party container stays possible later behind `IServiceProviderFactory`. Enforced by slice A5. |
| 2026-10-24 | A4: CI on GitHub-hosted `ubuntu-24.04`; third-party actions pinned to commit SHAs; actionlint runs as a CI job from a checksum-verified release binary (it ships no action); NuGet caching via `actions/cache` keyed on central package/props/csproj hashes (no lock files yet); frontend, contract-test and eval steps added by J1/C1/H3. |

## 9. Waivers

None.

## 10. Progress

| Slice | Status |
|---|---|
| A1 Repository + solution skeleton | ✅ |
| A2 Architecture tests | ✅ project-graph rules (layer references, adapter-only packages, SDK, naming, solution membership) + type rules (vendor/EF/infra dependencies, adapter visibility); violations proven to fail |
| A3 Binding mechanism | ✅ per-operation and per-tenant bindings, fail-fast startup validation (unit + host integration tests), binding health check |
| A4 CI | ✅ `ci.yml` (green on GitHub, run 40158426638): actionlint (checksum-verified binary), restore, Release build, `dotnet format --verify-no-changes`, architecture + unit + integration tests, TRX upload on failure; SHA-pinned actions, read-only token, per-ref concurrency, NuGet cache |
| B1 SharedKernel | ✅ typed-ID pattern + `TenantId`/`ITenantScoped`, `Entity`/`AggregateRoot` with raise/dequeue of domain events, `Result`/`Result<T>`/`Error`/`ValidationError`; `IIdGenerator` port + GUID v7 adapter bound via A3 |
| A5 DI conformance (P15) | ✅ host container validation in all environments; architecture tests for service location, `IConfiguration` injection, and registration placement; violations proven to fail |
| B2 Organization + Property | ✅ `Organization` (tenant-scoped, name rules, `OrganizationCreated`/`OrganizationUpdated`) and `Property` (`PropertyDetails`: name, IANA time zone, address, optional contact phone/email; all-field validation; `PropertyCreated`/`PropertyUpdated`; unchanged update is a no-op); `IOrganizationRepository`/`IPropertyRepository` interfaces |
| B3 Spaces + Accommodation | ✅ hierarchical `Space` (kind, code, description, guest directions; parent fixed at creation, same property); `AccommodationType` (name, max occupancy); `AccommodationUnit` (unit number, type + space in same property; renumber, change type); created/updated events; repository interfaces; shared `TextRules` in Domain.Properties |
| B4 OperatingHours | ✅ `OperatingHours` per subject (property/space/amenity/restaurant/service): weekly multi-interval hours, overnight intervals, all-day, closed days, date overrides, temporary closures (closure > override > weekly); `GetHoursFor(localDate)` with source + note, `GetStatusAt(instant, timeZone)` incl. previous-night spillover; full validation; aggregate `Version` (+1 per change) for grounding; `ValueList<T>` in SharedKernel |
| B5 Amenity, Service, Policy, ContentItem | ✅ `Amenity` (category, optional location space); `Policy` (type, title, authoritative content, effective dates, optional subject; Draft → Active → Retired, `IsInEffectOn`); `ContentItem` (type, title/body, optional subject, normalized tags; Draft ⇄ Published/Archived), kept separate from policy; `Service` in Domain.Services (machine code, category, requestable items with per-request quantity limits, target completion); shared `SubjectReference` and SharedKernel `TextRules` (incl. machine codes) |
| B6 Dining | ✅ `Restaurant` (name, cuisine, description, location `SpaceId`; hours via `OperatingHours` subject); `Menu` aggregate owning its `MenuItem`s (stable `MenuItemId`, optional `Money` price, category, normalized `DietaryTag`s, availability); `FindItems` dietary search (Demo 3); `SetItemAvailability`; one currency per menu; `MenuCreated`/`MenuUpdated`; `Money` + `MoneyRules` in SharedKernel; no POS fields |
| B7 PropertyEvent (activities) | ⏳ next |
