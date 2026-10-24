# Hospitality AI OS — Phase 1 Implementation Plan

**Governing principles:** [architecture-principles.md](architecture-principles.md) — this plan conforms to P1–P14; references like `(P5)` point there.
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

---

## 2. Port Catalogue (Phase 1)

Concrete ports required by Phase 1 under P5/P6.

### 2.1 Infrastructure Ports

| Port | Purpose | Local/dev adapter | Azure adapter | Other options (later) |
|---|---|---|---|---|
| `IUnitOfWork` (Ports), `I{Aggregate}Repository` (in each domain project) | Transactional persistence of aggregates | EF Core + SQLite | EF Core + Azure SQL / Azure Database for PostgreSQL | Cosmos DB, AWS RDS |
| `IReadStore` / query services | Read-side queries for APIs & capabilities | EF Core + SQLite | Same as above | Read replicas, Cosmos |
| `IOutboxStore`, `IInboxStore` | Transactional outbox / processed-event tracking | EF Core | EF Core | — |
| `IEventTransport` | Deliver events between dispatcher and handlers | In-memory channel | Azure Service Bus | Event Grid, Kafka, RabbitMQ, AWS SNS/SQS |
| `IEventStore` | Durable event log for replay/audit | EF Core table | EF Core / Event Hubs capture | EventStoreDB |
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
    HospitalityOS.Eventing/                # envelope, outbox dispatcher, inbox, retry, replay
    HospitalityOS.Capabilities/            # registry, executor, definitions
    HospitalityOS.Ai/                      # gateway, inference router, context builder, traces
    HospitalityOS.Workflows/               # Concierge, GuestService, HumanAction Router
    HospitalityOS.Authorization/
    HospitalityOS.IntegrationRuntime/      # provider registry/resolver, ExternalReference
  Adapters/
    HospitalityOS.Adapters.Local/          # in-memory/filesystem/dev implementations
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

### Milestone B — Canonical Domain Core (Epic 2)

**B1. SharedKernel** — typed IDs, `Entity`/`AggregateRoot` with raised domain events, `Result`/`Error`, validation errors, `TenantId`.
**B2. Organization + Property** — invariants, `PropertyCreated`/`PropertyUpdated`, validation errors (Journey 1).
**B3. Spaces + Accommodation** — hierarchical `Space`; `AccommodationType`, `AccommodationUnit`.
**B4. OperatingHours** — multiple intervals/day, closed days, temporary closures, date overrides, overnight intervals, time zone, versioned (Journeys 2, 16).
**B5. Amenity, Service, Policy, ContentItem** — policy separate from content (Journey 4).
**B6. Dining** — `Restaurant`, `Menu`, `MenuItem` with dietary attributes, availability; no POS fields.
**B7. PropertyEvent (activities)** — local-time ranges, category, audience, upcoming-only query.
**B8. Guest context** — `Guest`, `StayContext`, `Conversation`, `GuestMessage`.
**B9. ServiceRequest + HumanAction** — full spec §7 fields and an explicit state machine; illegal transitions rejected; double-complete is a no-op. `TaskAssignment`.

### Milestone C — Persistence + Event Backbone (Epic 3)

**C1. Persistence ports + contract tests** — `IUnitOfWork`, repositories, `IReadStore`; contract test suite.
**C2. EF Core adapter (SQLite)** — DbContext, configurations, tenant query filters, migrations; passes C1 contract tests; tenant isolation test.
**C3. Event envelope + contracts** — spec §8 envelope, versioned payloads for all §8 events, serializer.
**C4. Eventing ports + local adapters** — `IEventTransport`, `IOutboxStore`, `IInboxStore`, `IEventStore`; in-memory transport; contract tests.
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
| 2026-10-23 | AI-assisted onboarding from website/documents is out of Phase 1 scope; specified as Phase 2 candidate 2.1 in [phase-2-product-spec-hospitality-ai-os.md](phase-2-product-spec-hospitality-ai-os.md). Phase 1 keeps manual admin configuration. |
| 2026-10-23 | A1: `.slnx` solution format; xUnit v3 on Microsoft.Testing.Platform; built-in asserts (FluentAssertions licensing); aggregate repository interfaces live in domain projects so `Ports` stays SharedKernel-only. |
| 2026-10-23 | Solution structure rules and coding standards split into [solution-structure.md](solution-structure.md) and [coding-standards.md](coding-standards.md). |

## 9. Waivers

None.

## 10. Progress

| Slice | Status |
|---|---|
| A1 Repository + solution skeleton | ✅ |
| A2 Architecture tests | ✅ project-graph rules (layer references, adapter-only packages, SDK, naming, solution membership) + type rules (vendor/EF/infra dependencies, adapter visibility); violations proven to fail |
| A3 Binding mechanism | ⏳ next |
