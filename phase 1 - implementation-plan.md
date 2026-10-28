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
| `IIdentityProvider` / `ITokenValidator` | Staff/admin auth, guest session tokens (staff: host JWT bearer configuration, no port; see F1) | Dev JWT issuer (`dotnet user-jwts`) | Microsoft Entra ID / Entra External ID | Auth0, Cognito |
| `ICurrentActor` | Who is acting (user, agent, system) | From claims | From claims | — |
| `ICurrentTenant` | Tenant the request/message acts for; persistence filters on it (P10) | From claims / envelope | From claims / envelope | — |
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
    HospitalityOS.Domain.Capabilities/     # CapabilityInvocation
    HospitalityOS.Domain.Integrations/     # IntegrationConfiguration, ExternalReference
  Platform/
    HospitalityOS.Application/             # commands/queries per module, uses ports only
    HospitalityOS.Bindings/                # binding resolver, startup validation, health checks (2.4)
    HospitalityOS.Eventing/                # envelope, outbox dispatcher, inbox, retry, replay
    HospitalityOS.Capabilities/            # registry, executor, definitions
    HospitalityOS.Ai/                      # gateway, inference router, context builder, traces
    HospitalityOS.Workflows/               # Concierge, GuestService, HumanAction Router
    HospitalityOS.Authorization/
    HospitalityOS.IntegrationRuntime/      # provider registry, per-property resolver, configuration, health, external references, PMS stay import
    HospitalityOS.Integrations.Native/     # NativeCms: the platform's own content as a CMS provider (reads domains, so Platform, not Adapters)
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

**C1. Persistence ports + in-memory adapter + contract tests** — `IUnitOfWork`, repositories (`IReadStore` deferred to D3, see §8); reusable contract test suite; `HospitalityOS.Adapters.InMemory` passing it (atomic commit of aggregates + raised events, tenant filtering, optimistic concurrency). A2 allow-list updated so this persistence adapter may reference `Domains`.
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
| 2026-10-24 | B7: Event times are property-local wall-clock values (Kind dropped), like operating hours; "now" is converted with the property's time zone at query time. "Upcoming" means scheduled and not yet ended, so ongoing events are offered and past ones never are (Journey 8). An audience filter also returns `Everyone` events. Cancelled events are immutable (create a new one) and cancellation raises `PropertyEventUpdated` (spec §8 lists only Created/Updated). `BookingRequired` tells the AI to explain how to book without implying a reservation; Phase 1 has no booking capability. Location is a bare `SpaceId` (Activities may not reference Domain.Properties). |
| 2026-10-24 | B8: `GuestMessage` is a separate aggregate from `Conversation` so long conversations never load in full; the concierge reads recent messages through `ListRecentAsync`. Guest-sent messages raise `GuestMessageReceived` (spec §8), others `GuestMessageSent`; channel redelivery is absorbed via `ClientMessageId` (`FindByClientMessageIdAsync`, unique index in C2) (P8). Personal data is minimized: `Guest` keeps only display name + language, contact details stay with the channel/PMS, and guest events carry IDs only (no profile or message text) so PII stays out of the event log (P10, P11). Conversations start anonymous and gain one guest for life via `LinkStay` (same property, same guest). A message snapshots the conversation's guest/stay at recording time. Stays: unit required at check-in, arrival fixed after check-in, CheckedOut/Cancelled final; unit-in-property is an application-layer check (bare `AccommodationUnitId`). |
| 2026-10-25 | B9: `HumanAction` lives in Domain.Operations and references its parent by `ServiceRequestId` only; the Guest Service controller (I4) moves the request in response to HumanAction events, so the domains stay independent. Worker steps (accept/start/complete/fail) require the current assignee; reassignment is allowed only before acceptance and appends a `TaskAssignment`. Complete is allowed from Accepted (workers may skip Start; `StartedAt` is then set at completion). Cancel only before work starts (in-progress work is failed instead); Expire only before work starts and once the deadline passes. Every repeated transition is a successful no-op raising nothing; a duplicate Complete keeps the first completion (Journeys 14, 19). Illegal transitions return `InvalidState` (`human_action.transition_invalid`, `human_action.not_assignee`). Deadline = explicit deadline, else creation + SLA. `HumanActionCancelled`/`HumanActionExpired` added beyond spec §8 (P1). `ServiceRequest.Exception` keeps failed work unresolved and restartable (Journey 20); `DueAt` is internal and never quoted to guests as an ETA (Journey 12). One request per source guest message (`FindBySourceMessageAsync`, unique index in C2) prevents duplicate deliveries (Journey 9). |
| 2026-10-25 | C1: `IUnitOfWork.CommitAsync` returns `Result<CommitReceipt>`: the committed `CommittedEvent`s (tenant, aggregate type/ID/version, event), which C5 writes to the outbox in the same transaction. Conflicts are expected failures, not exceptions: `persistence.concurrency_conflict` (aggregate saved by another scope since it was loaded; `AggregateRoot.Version` is the concurrency token) and `persistence.duplicate_key` (ID or unique key taken); a failed commit saves nothing and leaves events on the aggregates. Repositories track what they return (one instance per aggregate per scope, no `Update` call); `Find…`/`List…` queries match committed data only, as in a database. The five unique keys promised by B3–B9 (unit number and service code per property, case-insensitive; one hours record per subject; `ClientMessageId` per conversation; one request per source message) are enforced by the in-memory adapter too, so racing scopes cannot create duplicates (P8). `ICurrentTenant` (Ports) supplies the tenant; adding another tenant's aggregate throws (programming error). In-memory snapshots are reflection-based deep copies so uncommitted changes never leak and the domain gains no copy code. `IReadStore` deferred to D3, where the real read queries are defined (a generic expression-based port would pass in memory and fail to translate on SQLite). Host persistence selection deferred to C2, when there are two adapters to choose between. A2 tightened: only persistence adapters (`Adapters.InMemory`, `Adapters.EfCore*`) may reference `Domains`. |
| 2026-10-25 | C2: EF Core maps the domain aggregates directly (no separate data models). EF Core cannot pass value objects to constructors, so `Entity`/`AggregateRoot` gained a protected parameterless constructor and each aggregate a private EF-only one (coding standards updated). Mapping rule: scalar members are columns; a top-level details record is a complex type (`Details_Name`, …); records nested inside another record and all lists are JSON columns (camelCase, enums as names, IDs as plain GUIDs), because EF Core cannot construct a record holding another record. Typed IDs are GUID columns and enums are stored by name, both by convention. EF Core cannot index complex-type columns, so unique keys inside value objects get hidden key columns (`UnitNumberKey` upper-cased, `CodeKey`, `SubjectKey`, `SourceMessageId`) recomputed on every save and backed by unique indexes; `ClientMessageId` uses its own column. Tenant isolation is a query filter on every aggregate reading `ICurrentTenant` per query. `Version` is the concurrency token. Provider specifics stay in the provider project: SQLite stores `DateTimeOffset` as UTC ticks (it cannot sort them) and its unique-violation codes map to `persistence.duplicate_key`. Migrations live in `Adapters.EfCore.Sqlite` (generated by the `dotnet-ef` local tool, treated as generated code, exempt from the adapter visibility rule) and are applied by deployment or `dotnet ef database update`, never by the app at startup; a test fails when the model changes without a migration. The API host chooses persistence from `Persistence:Provider` (`InMemory` default, or `Sqlite` with `Persistence:ConnectionString`), one store for the whole host so commits stay atomic; unknown provider or missing connection string stops startup. Until F1 the host's `ICurrentTenant` throws if used. |
| 2026-10-25 | C3: published payloads are separate contract types in `HospitalityOS.Contracts` (not the domain events), so domain refactors cannot silently change what consumers read and modules can later split into services (P9). The type name is the contract (`HumanActionCompletedV1` = `HumanActionCompleted` v1); a published version never changes, a breaking change adds `V2` beside it. Payloads use SharedKernel IDs where they exist, other IDs as GUIDs, enums as their domain names, shared `*Data` value records, and never guest personal data (P10). The envelope adds `aggregateVersion` to spec §8 (orders one aggregate's events for replay); `organizationId` is set only when the event identifies it; `propertyId` comes from the aggregate via new `IPropertyScoped` (SharedKernel), carried on `CommittedEvent`. Mapping lives in Eventing (`EventContractMapper`, generated from one table). Wire format: camelCase, enums as names, IDs as plain GUIDs, strict reading (missing required values rejected); unknown event types or versions fail to deserialize so they are retried or dead-lettered. Guards: a test requires a sample for every domain event type, and each payload must match its committed JSON sample (`tests/.../Eventing/Contracts/Samples`, rewritten only with `HOSPITALITYOS_UPDATE_EVENT_SAMPLES=1` and reviewed). The §8 events no aggregate raises yet (`GuestQuestionAnswered`, `CapabilityInvocation*`, `GuestNotification*`, `AITraceCreated`) get their payloads in the slices that raise them (H1, D2, I5, G6) rather than being guessed now. The host now binds `IIdGenerator` to GUID v7 (event IDs) and registers Eventing. |
| 2026-10-25 | C4: eventing ports carry `EventMessage` (serialized envelope + routing fields: event ID, type, version, tenant, aggregate, correlation, recorded time), since ports cannot see Contracts. Two kinds of operation, by design: writes that must be atomic with business changes are staged and saved by `IUnitOfWork.CommitAsync` (`IEventStore.Append`, `IInboxStore.Record`); dispatcher operations take effect immediately and never save the scope's pending changes (`IOutboxStore` claim/mark/retry/dead-letter, via `ExecuteUpdate`/`ExecuteDelete` in EF). Appending writes the append-only log and the outbox row in the same transaction, so nothing recorded goes unpublished; outbox rows are deleted once dispatched, the log is never changed (P10). The outbox is claimed oldest first under a lease (conditional update, safe with several dispatchers); delivery is at-least-once. Inbox records share the handler's commit, so racing duplicate deliveries fail with `persistence.duplicate_key` and save nothing (Journey 19). Eventing stores are system-level (not tenant-filtered) and registered with persistence because they share its transaction. `IEventTransport`: every subscription gets every event published while it exists, in order; handler failures are redelivered up to a max count, then dead-lettered (readable for L1) without blocking later events. The in-memory transport lives in `Adapters.Local`, bound as `EventTransport` → `InMemory` (settings `MaxDeliveryCount`, `RedeliveryDelay`); it keeps nothing across restarts. Migration `AddEventStoreOutboxInbox`. C5 wires commits to append envelopes for every committed domain event. |
| 2026-10-25 | C5: every commit records its domain events: both units of work build the committed events before saving, turn each into a message through the new `IEventMessageFactory` port (implemented by Eventing: envelope + contract + serialization), and save them to the event store and outbox in the same transaction as the aggregates. The messages exist only for that commit attempt (EF detaches them on failure), so a failed commit records nothing and a retry records each event exactly once; events leave the aggregates only after success. Until C7, `ScopedEventOrigin` gives each scope a new correlation ID and actor `System`. `OutboxDispatcher` claims a batch, publishes each event to the bound `IEventTransport`, and removes it; a failed publish is retried with exponential back-off (`InitialRetryDelay` doubling to `MaxRetryDelay`) and dead-lettered after `MaxAttempts`, without holding up the rest. `EventDispatcherService` (hosted) runs passes back-to-back while batches are full, otherwise every `PollInterval`; a failing pass is logged and retried, never stops the host. Settings in `Eventing:Dispatcher`, validated at startup; `Enabled` lets hosts opt out (several may run, leases keep them apart). The dispatcher gets its per-pass DI scope through a factory registered in `DependencyInjection`, so no class holds the container (P15). Eventing now references the vendor-neutral `Microsoft.Extensions.Hosting/Logging/Options` abstractions. |
| 2026-10-25 | C6: handlers implement `IEventHandler<TPayload>` (one published payload version) and register with `AddEventHandler<TPayload, THandler>(name)`; the name is the handler's stable identity (transport subscription and inbox key), so renaming makes replay re-run it; a handler of several event types registers once per type. CA1711 is suppressed on `IEventHandler` only: it means a platform event handler, not a .NET event delegate. `EventSubscriptionHost` subscribes each handler; `EventProcessor` handles each delivery in its own scope: skip if the inbox says processed, deserialize, set the scope's tenant from the envelope, continue the flow (correlation = handled event's, causation = its ID, actor = `Agent`/handler name), run the handler, record the inbox entry, commit both together. A conflict on commit (inbox key or an aggregate both deliveries changed) that the inbox confirms means another delivery won the race: treated as a duplicate, not a failure; other failures are thrown so the transport redelivers and then dead-letters. New `TenantContext` (Ports/Tenancy) replaces the host's placeholder: set once per scope from claims (F1) or the envelope; reading it unset throws. `IEventReplayer` publishes recorded events again (by position range, tenant, correlation, time) straight to the transport; the inbox makes it re-run only what did not happen. `IInboxStore.MaxHandlerLength` = 200. |
| 2026-10-26 | C7: `OperationContext` (Ports/Execution, scoped) is the ambient context of one operation: correlation ID, causation ID, and `Actor` (type + ID). The HTTP pipeline joins the caller's `X-Correlation-Id` (valid non-empty GUID) or starts a new flow, returns it in the response header, and adds it to every log scope; the event processor joins the handled event's flow (causation = its ID, actor = `Agent`/handler name); otherwise the first read starts a new GUID v7 flow, actor `System`. Joining a different flow after one started, or switching actors, is a programming error. Events, audit records, and (later) capability invocations and AI traces take their origin from it; Eventing's private origin is gone. `ActorType` moved from Contracts to SharedKernel/Execution beside the new `Actor` record (wire format unchanged). Audit: the event store already records every state change; `AuditRecord` (Ports/Auditing) covers what events do not (capability invocations, administrative actions, sensitive reads, failed and denied attempts). `IAuditLog` is append-only (no update or delete; database rights enforced at K1), tenant-scoped (reads see only the current tenant; appending another tenant's record throws), and staged with the unit of work, so a record of a change is saved exactly when the change is; recording a failure means committing a scope without other changes. `IAuditTrail` (Application) fills tenant, actor, correlation, causation, time, and ID, and truncates over-long detail rather than losing the record. In-memory and EF Core (`AuditLog` table, tenant query filter, migration `AddAuditLog`) adapters pass one contract suite. The host now calls `AddApplication()` and runs `CorrelationMiddleware` first. |
| 2026-10-26 | D1: new platform project `HospitalityOS.Capabilities`. A capability is a class implementing `ICapability<TInput, TOutput>` with a static `Descriptor` (name, version, description, authorization, confirmation policy, side effects, idempotency, audit classification: spec §11); `AddCapability<T>()` builds its `CapabilityDefinition` and generates input/output JSON Schema from the types, so an invalid declaration or a duplicate name+version fails at startup. Rules: name `<Area>.<Action>` PascalCase, at most 64 characters (AI providers' tool-name limit; G4 maps the dot); version from 1, a breaking I/O change adds a version beside the old one; description required (agents choose tools by it); at least one allowed caller; read-only capabilities are inherently idempotent and never need confirmation; external effects require an idempotency key (P8). Authorization is a set of `CapabilityCaller`s (System, Agent, Guest, Worker, Manager, Admin: the plan §3 roles split out), checked by the executor in D2; how callers are derived from claims comes with F1. The metadata enums, `CapabilityAuthorization`, and the API record `CapabilityDescription` live in Contracts. The wire format moved from Eventing to `Contracts/Serialization/ContractJson` (shared by events, capabilities, and the API's HTTP JSON) and now rejects numbers sent as strings (no published event changed; golden samples pass). Capability I/O additionally rejects unknown members (`additionalProperties: false`), so an agent's invented argument is refused; a constructor parameter without a default is required even when nullable; `[Description]` becomes the schema description. Schemas come from the BCL `JsonSchemaExporter` over the same options the serializer uses, with typed IDs described as UUID strings and `ValueList<T>` as arrays. `CapabilityRegistry` finds by exact (case-sensitive) name, latest version unless one is given. `GET /api/v1/capabilities` and `/api/v1/capabilities/{name}?version=` publish every spec §11 field; no authentication until F1 (definitions are not tenant data). The 14 spec §11 capabilities are declared in the slices that implement them (D3 reads, I1/I2/I5 writes) rather than guessed now, as with C3's events, so the shipped host lists none yet. |
| 2026-10-26 | D2: `ICapability<TInput, TOutput>.ExecuteAsync` returns `Result<TOutput>`; capabilities are scoped, change state only through repositories, and never commit. `ICapabilityExecutor` (scoped) runs lookup → authorize → confirmation → idempotency key → validate input → replay → run → validate output → one commit of the capability's changes, a `CapabilityInvocation`, its events, and an audit record. Authorization comes before input validation, so a refused caller learns nothing about the input. Each attempt runs in its own DI scope carrying the caller's tenant, flow, and actor, so the caller's staged work is never committed by it and a failed attempt's changes are discarded. `CapabilityInvocation` is an aggregate in new domain module `Domain.Capabilities` (spec §6), so its `CapabilityInvocationRequested/Succeeded/Failed` events (payloads carry no input or output, P10) go through the existing outbox; persisted in both adapters (migration `AddCapabilityInvocations`). It keeps the input as a SHA-256 hash of its canonical wire form and the output for replay. Idempotency: the key (trimmed, at most 200 characters) is unique per tenant and capability among succeeded invocations only, so a failed attempt can be retried with the same key; a repeated key with the same input returns the stored result without running (audited as a replay); with different input it is `capability.idempotency_key_reused`. Two racing attempts with one key: if they touch the same aggregate the loser gets a concurrency conflict and its retry replays the winner; if not, the unique key lets one commit and the loser returns the winner's result. Rejections (`capability.unknown`, `forbidden`, `confirmation_required`, `idempotency_key_required`, `invalid_input`) are audited (Denied or Failed) without an invocation record; failures after acceptance (the capability's own error, `invalid_output`, `crashed`, commit conflicts) are recorded as a failed invocation in a fresh scope, and a thrown exception is rethrown after recording. Audit detail holds the version, the error, and, for `Routine` capabilities only, the canonical input; `Sensitive` ones record neither input nor error message. Callers come from `OperationContext.Actor`: `Actor` gained an optional `StaffRole` (Worker, Manager, Admin) and staff without a role may invoke nothing; the audit log stores the role (`AuditLog.ActorRole`). `ErrorKind.Forbidden` added. No HTTP endpoint invokes capabilities yet: without authentication (F1) every request would act as `System`. |
| 2026-10-26 | D3: the seven spec §11 reads are built-in capabilities registered by `AddBuiltInCapabilities()` (host calls it): `Property.GetProperty` (details, time zone and local now, amenities and restaurants with their IDs, so an agent can find what to ask about), `Property.GetOperatingHours` (subject + property-local date, default today; weekly/override/closure basis, note, open-now including overnight spill-over), `Property.GetPolicy` (active and in effect today, by type and by subject; property-wide policies always included), `Property.SearchContent` (published content, keyword-ranked: title/tag hits 3, body hits 1, hyphens ignored, common words dropped; in memory over one property, replaced by `ISearchIndex`/vectors in G5 behind the same contract), `Dining.SearchRestaurants` (text, dietary need = at least one available dish with all tags, today's hours), `Dining.GetMenu` (all items, or only available items matching dietary tags), `Events.SearchPropertyEvents` (upcoming/ongoing via `PropertyEvent.SelectUpcoming`, window/category/audience/limit; a `from` in the past never resurfaces past events). Grounding (P4): every answer item carries `SourceReference` (record type, ID, version); "no authoritative source" is an empty list or a `*.not_found` error, and descriptions tell agents not to guess. All reads: every caller, no side effects, inherently idempotent, routine audit. Inputs and outputs are capability contracts in `Contracts/Capabilities/{Properties,Dining,Activities}` (separate from event payload records, so the two evolve independently); domain enums are mirrored by contract enums with identical member names (test-enforced) and converted by name; input fields carry `[Description]`, so the published schemas explain them to agents. `IReadStore` is not introduced: reads use specific repository queries added to six ports (`ListForProperty` for amenities, restaurants, and menus; `ListForRestaurant`; `ListActiveForProperty` for policies; `ListPublishedForProperty` for content; `ListScheduledEndingAfter` for events, filtered in the database so past events do not accumulate in every read), implemented and contract-tested on both adapters, returning no particular order (adapters order GUIDs differently; capabilities sort). Test capabilities use a `Test.` area so they never collide with built-ins. |
| 2026-10-26 | E1: new Core project `HospitalityOS.IntegrationContracts` (SharedKernel only, no packages) holds the six spec §10 ports: `IPmsConnector` (get a stay, list in-house stays), `ICmsConnector` (list all published content; an absent item is unpublished), `IPosConnector` (list outlets, list an outlet's menus with prices and availability), `IMessagingConnector` (send at most once per idempotency key, replay returns the original receipt), and placeholders `ICrsConnector` (availability read) and `IPaymentConnector` (payment status read; never moves money). Phase 1 connectors are read-only except messaging. A connector instance serves one property, so methods take no property ID; E2's per-property resolver hands out the instance configured for that property. Provider identifiers are `ExternalId` (opaque string, kept exactly, ordinal, at most 256 characters), never canonical IDs; E2 maps them through `ExternalReference`. Results are connector-specific canonical records (`PmsStay`, `CmsContent`, `PosMenuItem`, ...), not domain types (Core may not reference Domains); their enums mirror the domain's by name (`StayStatus`, `ContentType`, `MessageChannel` ↔ `ConversationChannel`), test-enforced. Guest data is the canonical profile's minimum (P10); dietary attributes are platform-owned, not POS data. Expected failures are `IntegrationErrors` (`integration.not_found`, `integration.rejected`); outages throw. Architecture tests pin the six interface names, allow only System, collections, tasks, SharedKernel and the contracts' own types in public signatures, and require async methods ending in a `CancellationToken`. Health and connector contract suites come with the runtime (E2) and first implementations (E3). |
| 2026-10-27 | E2: provider selection is tenant data, not app configuration: "on A3" is realized by reusing A3's pattern (named factories, per-instance caching, health probes via `IAdapterHealthProbe`) rather than its appsettings, which are read once at startup and cannot vary by property. New domain module `HospitalityOS.Domain.Integrations`: `IntegrationConfiguration` (property + `ConnectorKind` + provider name + non-secret settings + Active/Disabled; one per property and kind, unique key on both adapters; secrets are only named in settings, P10) and immutable `ExternalReference` (property + provider + canonical type name + provider ID kept exactly → canonical GUID; unique per property, provider, type and provider ID; a canonical record may hold IDs from several providers, so a swap adds references and keeps the old ones, Journey 18). Events `IntegrationConfigurationCreated`/`Updated` (settings not published) and `ExternalReferenceCreated`, with V1 payloads and samples. `IIntegrationProvider<TConnector>` (spec `IntegrationProvider`: `Name`, `ValidateSettings`, `Create(ConnectorContext)`) lives in IntegrationContracts, because integration projects may reference only Core; providers register as singletons. New platform project `HospitalityOS.IntegrationRuntime`: `ProviderRegistry<TConnector>` (singleton; names unique ignoring case; one connector per configuration, recreated when its version changes; replaced connectors are not disposed while requests may hold them); `IIntegrationResolver<TConnector>` (scoped, open generic, so no service locator) returns the connector with its provider name or `integration.not_configured` / `integration.disabled` / `integration.provider_unavailable`; `IIntegrationConfigurator` (lists providers; configures or swaps by registered name, ignoring case, after the provider validates the settings; enables/disables; stages changes, caller commits); `IIntegrationHealth` (per-property report per configured connector; disabled ones not probed; failures reported, never thrown); `IExternalReferences` (find, list per canonical record, idempotent link, `integration.reference_conflict`). Health is per property rather than a host health check, because configurations are tenant data the host check cannot read across tenants. `TextRules.IsCode` made public for setting names. Migration `AddIntegrations`. The host calls `AddIntegrationRuntime()`; it has no providers until E3. |
| 2026-10-27 | E3: `HospitalityOS.Integrations.Mock` (Adapters) ships `MockPms`, `MockPos`, `MockMessaging`. Adapters may expose only DI extensions and `*Options`, so mock PMS stays and POS outlets/menus come from `MockPmsOptions`/`MockPosOptions` (per property, read when a connector is created; F4 seeds them), and `MockMessagingOptions` lists unreachable recipients; the messaging gateway is an internal singleton that accepts each idempotency key once per property, survives connector re-creation, and keeps a delivery log that tests read through `InternalsVisibleTo`. `NativeCms` (the platform's own published content items: provider ID = canonical ID, revision = aggregate version) must read the Properties domain, which adapters may not, so it lives in a new Platform project `HospitalityOS.Integrations.Native` rather than under Adapters (plan layout updated); it reads in its own scope for the connector's tenant through a factory registered in its `DependencyInjection` (P15). E1's `CmsContent.UpdatedAt` became an opaque `Revision` (ETag/revision/version), since native content has no timestamp and change detection only needs equality. Connector contract suites (P13) in PortContractTests (`Pms`, `Pos`, `Cms`, `Messaging`; any future vendor connector must pass them) run against the four providers. Journey 18 is shown end to end with a PMS stay import (`IPmsStayImport`, runtime): in-house PMS stays become canonical guests and checked-in stays, with the provider's IDs linked; an unlinked PMS stay is matched to the in-house stay in the same unit with the same arrival (so a provider swap, or a natively created stay, keeps its canonical IDs and gains the new provider's IDs); everything that cannot apply (no or unknown unit, unit occupied, two PMS stays in one unit, ended stay, arrival changed after check-in, PMS guest linked to another guest) is skipped with a reason before anything changes; departures are not inferred from absence; staged, caller commits. New query `IStayContextRepository.FindInHouseInUnitAsync` on both adapters. The host registers the mock and native providers; properties use them only when configured. |
| 2026-10-27 | F1: staff authentication is ASP.NET Core JWT bearer in the host, configured under `Authentication:Schemes:Bearer` (issuer, audiences, signing keys, or an authority such as Microsoft Entra ID), so the identity provider is configuration; locally `dotnet user-jwts create --role Admin --claim tenant_id=<guid>` issues tokens, and with nothing configured every token is refused. The host is the token-validating adapter, so no `ITokenValidator` port is needed for staff (guest session tokens, J/H, may add one). A token must carry `sub`, a GUID `tenant_id`, and at least one `role` naming a `StaffRole` (the highest wins); otherwise it is rejected (401), so no request runs without a tenant. `CallerContextMiddleware` (after authentication, before authorization) sets the request's `TenantContext` and `OperationContext` actor (`Actor.Staff`), so persistence filters on the token's tenant and events and audit records name who acted. Policies: every staff role reads; only `Admin` creates or changes. Denied attempts by known callers are audited (`Denied`, under the endpoint's action name); anonymous ones are not (no tenant to record them in). Application services `IOrganizationAdministration` and `IPropertyAdministration` (Application) validate, check the organization is in the caller's tenant, and commit each change with its audit record (`AuditedCommit`); rejected commands are audited as `Failed` with the error code and save nothing else; reads are not audited. A commit conflict is returned unaudited (rare; recording it would need a fresh scope). API DTOs live in `Contracts/Api` with every request field optional, so missing fields are reported by validation field by field (RFC 9457 problem details with `code` and `fieldErrors`) instead of failing JSON binding; errors map one status per `ErrorKind`. Records carry `version`, returned as the `ETag`; `If-Match` makes an update conditional (stale: 412, malformed: 400). Routes: `/api/v1/organizations` and `/api/v1/properties` (`?organizationId=` filter), POST/GET/GET by id/PUT. New repository queries `IOrganizationRepository.ListAsync` and `IPropertyRepository.ListAsync(organizationId?)` on both adapters. Isolation is per tenant; restricting staff to particular properties within a tenant is deferred. The capability catalogue and `/health` stay anonymous. |
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
| B7 PropertyEvent (activities) | ✅ `PropertyEvent` in Domain.Activities: title, category, audience, property-local start/end, description, location `SpaceId`, optional price, `BookingRequired`; Scheduled → Cancelled; `SelectUpcoming(events, now, timeZone, filter)` excludes past and cancelled, keeps ongoing, filters by window/category/audience (Everyone matches any audience), earliest first; created/updated events |
| B8 Guest context | ✅ `Guest` (minimal profile: display name, normalized language tag); `StayContext` (property, guest, local arrival/departure, unit; Reserved → InHouse → CheckedOut / Reserved → Cancelled; room moves, extensions); `Conversation` (property, channel, Open → Closed, anonymous until linked to a stay); `GuestMessage` as its own immutable aggregate (sender, text, channel `ClientMessageId` for de-duplication) raising `GuestMessageReceived`/`GuestMessageSent`; Guest/Stay/Conversation/Message IDs in SharedKernel |
| B9 ServiceRequest + HumanAction | ✅ `ServiceRequest` (Domain.Services): validated against its `Service` (guest-requestable, item exists, quantity cap, description when itemless), origin (guest/stay/conversation/source message), due time from target; Open → InProgress → Completed, Exception (unresolved), Cancelled; complete-twice no-op. `HumanAction` (Domain.Operations): all spec §7 fields; explicit state machine Requested → Assigned → Accepted → InProgress → Completed / Failed / Cancelled / Expired with assignee-only worker steps, repeat = no-op, illegal = InvalidState; `TaskAssignment` history; one event per transition. `ServiceRequestId`, `StaffMemberId`, `ActionLocation` in SharedKernel |
| C1 Persistence ports + in-memory adapter + contract tests | ✅ `IUnitOfWork` (atomic commit, `CommitReceipt` of events, concurrency/duplicate-key conflicts), `ICurrentTenant`; `HospitalityOS.Adapters.InMemory` (snapshot store, per-scope tracking, 19 repositories, unique keys); `HospitalityOS.PortContractTests` with abstract repository (×19 aggregates), query and unique-key suites, failures proven by breaking the adapter; A2 restricts `Domains` references to persistence adapters |
| C2 EF Core adapter (SQLite) | ✅ `HospitalityDbContext` mapping all 19 aggregates (complex types, JSON columns, typed-ID/enum conventions, hidden key columns with unique indexes, tenant query filters, `Version` concurrency token); `EfUnitOfWork` + 19 repositories; `Adapters.EfCore.Sqlite` (UTC-ticks timestamps, unique-violation mapping, `InitialCreate` migration, design-time factory, `dotnet-ef` local tool); the C1 contract suites pass unchanged on SQLite (544 contract tests across both adapters), failures proven by breaking the adapter; migration drift and idempotent-migrate tests; host persistence selection |
| C3 Event envelope + contracts | ✅ `EventEnvelope` (spec §8 + `aggregateVersion`), `ActorType`, 45 versioned payloads (`*V1`) + 21 value records in `HospitalityOS.Contracts`; Eventing: `EventContractMapper`, `EventTypeRegistry` (names from type names, duplicates fail at startup), strict JSON `EventSerializer`, `EventEnvelopeFactory` (GUID v7 IDs, recorded time, origin); `IPropertyScoped` + `CommittedEvent.PropertyId`; committed JSON sample per event with drift test proven to fail; host binds `IIdGenerator` |
| C4 Eventing ports + local adapters | ✅ `EventMessage`, `IEventStore` (staged append → log + outbox; paged/filtered reads), `IOutboxStore` (leased claim, mark, retry, dead-letter), `IInboxStore` (staged record, check), `IEventTransport` (subscriptions, redelivery, dead letters); in-memory and EF Core (SQLite, migration) stores sharing the unit of work's transaction; in-memory transport bound via A3; contract suites for all four ports (stores on both adapters), failures proven by breaking the adapters; host binds the transport |
| C5 Outbox dispatcher | ✅ commits record envelopes for every domain event in the same transaction (`IEventMessageFactory`, exactly once across retries, both adapters); `OutboxDispatcher` (batch claim, publish, mark; exponential back-off; dead-letter after max attempts) + hosted `EventDispatcherService`; validated `Eventing:Dispatcher` options; end-to-end test commit → dispatcher → subscriber in the real host, proven to fail with the dispatcher off |
| C6 Idempotent handlers + replay | ✅ `IEventHandler<TPayload>` + `AddEventHandler`; per-delivery processing with inbox skip, tenant from envelope, correlation/causation continued, commit with inbox record, lost races treated as duplicates; `EventSubscriptionHost`; `IEventReplayer`; `TenantContext`; host tests: effect once under redelivery and replay (Journey 19), concurrent race, retry-then-success, dead letter without blocking, flow continuation; failures proven by breaking the processor |
| C7 Correlation/causation + audit | ✅ `OperationContext` (correlation, causation, actor) used by events and audit; `CorrelationMiddleware` (`X-Correlation-Id` in/out, log scope); handler scopes continue the flow; `Actor`/`ActorType` in SharedKernel; append-only, tenant-scoped `IAuditLog` (in-memory + EF Core/SQLite, migration) with contract suite; `IAuditTrail` in Application; failures proven by breaking tenancy and correlation |
| D1 Capability definitions + registry | ✅ `HospitalityOS.Capabilities`: `ICapability<TInput, TOutput>` + static `CapabilityDescriptor` (all spec §11 fields), `CapabilityDefinition` (startup validation of names, versions, callers, and policy consistency; JSON Schema in/out from the wire format with typed IDs, lists, enums, nullability, descriptions, no extra members), `CapabilityRegistry`, `AddCapabilities`/`AddCapability<T>`; shared `ContractJson` wire format in Contracts (strict numbers); `GET /api/v1/capabilities[/{name}]`; failures proven by breaking validation, schemas, strictness, registry, registration, and host JSON |
| D2 Capability executor | ✅ `ICapabilityExecutor`: lookup, caller authorization (actor → `CapabilityCaller`, staff by `StaffRole`), confirmation, idempotency key, strict input validation, replay of a used key (different input = conflict), run in an isolated scope, output validation, one commit of changes + `CapabilityInvocation` + events + audit; rejections audited, failures recorded with changes discarded, crashes recorded and rethrown, racing keys resolved to one effect; `Domain.Capabilities` (`CapabilityInvocation`, 3 events + `V1` payloads + samples) persisted in both adapters (migration `AddCapabilityInvocations`, unique succeeded-key index, contract tests); `ErrorKind.Forbidden`; audit stores the actor role; failures proven by breaking authorization, confirmation, replay, key reuse, discard-on-failure, audit redaction, race recovery, and output validation |
| D3 Read capabilities | ✅ `Property.GetProperty`, `Property.GetOperatingHours`, `Property.GetPolicy`, `Property.SearchContent`, `Dining.SearchRestaurants`, `Dining.GetMenu`, `Events.SearchPropertyEvents` as built-in capabilities with `SourceReference` (type, ID, version) on every answer; contract I/O records and mirrored enums in Contracts with schema descriptions; six new repository queries on both adapters with contract tests; `/api/v1/capabilities` now lists the seven; host tests against a seeded Aurora Grand at a fixed time (holiday and closure hours, policy status and effective dates, unpublished content, dietary filters, past and cancelled events, tenant isolation); failures proven by breaking each filter, sources, open-now, search normalization, and adapter queries |
| E1 Integration contracts | ✅ `HospitalityOS.IntegrationContracts`: `IPmsConnector`, `ICmsConnector`, `IPosConnector`, `IMessagingConnector`, placeholder `ICrsConnector` and `IPaymentConnector`; property-bound instances; `ExternalId` for provider identifiers; canonical records with domain-mirrored enums; `IntegrationErrors`; architecture tests for the spec connector set, canonical-only signatures, and async + `CancellationToken` methods. |
| E2 Integration runtime | ✅ `HospitalityOS.Domain.Integrations` (`IntegrationConfiguration`, `ExternalReference`, events + V1 payloads, both adapters, migration `AddIntegrations`); `IIntegrationProvider<TConnector>` in IntegrationContracts; `HospitalityOS.IntegrationRuntime`: provider registry, per-property `IIntegrationResolver<TConnector>`, `IIntegrationConfigurator`, `IIntegrationHealth`, `IExternalReferences`; host wired; provider swap keeps canonical IDs and references (integration-tested). |
| E3 Connectors | ✅ `MockPms`, `MockPos`, `MockMessaging` (`HospitalityOS.Integrations.Mock`), `NativeCms` (`HospitalityOS.Integrations.Native`, Platform); connector contract suites for PMS, POS, CMS, messaging; PMS stay import (`IPmsStayImport`) with unit-and-arrival matching; Journey 18 test: MockPms → another provider by configuration only, canonical stay and guest IDs unchanged, both providers' IDs in `ExternalReference`. |
| F1 Organization/Property APIs | ✅ JWT staff authentication (tenant + actor from claims, roles Admin/Manager/Worker, denied attempts audited); `/api/v1/organizations` and `/api/v1/properties` (create, list, get, update; field-by-field validation errors; ETag/If-Match); administration services committing each change with its audit record; list queries on both adapters; Journey 1 acceptance covered end to end over HTTP. |
| F2 Spaces, Amenities, Hours, Policies, Content APIs | ⏳ next |
