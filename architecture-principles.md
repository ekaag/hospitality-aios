# Hospitality AI OS — Architecture Principles

**Status:** Living document — applies to **every phase**.
**Audience:** Product, Engineering, coding agents, QA, reviewers.
**Companion standards (also apply to every phase):** [solution-structure.md](solution-structure.md) — where code lives and how projects are named/wired · [coding-standards.md](coding-standards.md) — C# naming, style, and design rules.

These principles are non-negotiable defaults. Every phase product spec and implementation plan must conform to them and reference them by ID (e.g. `P3`). A phase may deviate only through an explicit, recorded waiver (see §4).

---

## 1. Product & Operating Model Principles

### P1 — AI-First, Event-Driven
Every meaningful state change emits a canonical, versioned domain event. Autonomous components subscribe to events, evaluate state, and decide what happens next. The operating loop is:

```text
Event → evaluate state → decide → digital capability OR HumanAction → resulting event → continue
```

The system evolves toward **Sense → Understand → Decide → Act → Observe → Adapt**.

### P2 — Humans Are Physical-World Executors
Humans participate only when physical work, regulation, explicit policy, or an unresolved exception requires it. Humans are not the workflow engine; normal workflow progression requires no manager action. Physical work is always represented as a first-class `HumanAction` whose completion/failure re-enters the system as an event.

### P3 — Canonical Domain Ownership
The platform owns canonical entities and IDs. External systems supply data and actions but never define the domain model. External/vendor identifiers live only in `ExternalReference`. No vendor-specific fields in domain types.

### P4 — Grounded, Honest AI
AI answers only from authoritative platform data. Explicit uncertainty is preferred over fabrication. AI never invents policies, prices, availability, ETAs, or confirmations, and never implies an action happened unless a capability actually performed it.

---

## 2. Architecture Principles

### P5 — Everything Behind an Interface (Ports & Adapters)
Every external dependency — cloud service, database, messaging, storage, identity, model provider, hospitality system — is reached only through a **platform-owned interface (port)**.

- **Ports** describe *what* the platform needs using platform/canonical types only — never SDK types.
- **Adapters** implement ports for one technology, each family in its own project.
- **Bindings** (configuration) select which adapter serves each port, and can differ **per operation** and **per tenant/property** — e.g. events on one cloud, blobs on another; one model for extraction, another for conversation.
- The **composition root** (host) is the only code that references adapters. Swapping a provider is a configuration change plus, at most, a new adapter project. Domain, workflow, capability, and AI code never change.

Applies uniformly to three port families: **infrastructure**, **inference** (P6), and **hospitality integrations** (PMS, CMS, POS, messaging, CRS, payments).

### P6 — Inference as Tasks, Not Models
Code requests an inference **task** (e.g. `concierge.answer`, `intent.classify`, `content.embed`), never a specific model or vendor. Configuration binds each task to a provider + model + parameters, with optional fallback chains and per-tenant overrides. Prompts are versioned independently of providers. Every inference call records provider, model, prompt version, and usage.

### P7 — No Direct AI Mutation; Capabilities Only
LLMs and agents never mutate state directly. All actions execute through **registered capabilities** that declare input/output schemas, authorization, confirmation policy, side effects, idempotency, and audit classification. Agents get an allow-list of capabilities — no raw SQL, no arbitrary HTTP, no vendor API calls. Unknown capabilities are rejected.

### P8 — Idempotency by Default
Every event handler, capability invocation, external command, migration, and deployment tolerates retries and at-least-once delivery. A logical effect happens exactly once even when its trigger is delivered many times; duplicates are observable but harmless.

### P9 — Strict Dependency Direction
```text
Domain           ─► SharedKernel only
Ports/Contracts  ─► SharedKernel only
Application/AI/Workflows ─► Domain + Ports
Adapters         ─► Ports + their own vendor SDK   ← the only place SDKs appear
Host             ─► everything (composition root)
```
Modules communicate via events and ports, not each other's internals, so the system can start as a modular monolith and split into services without redesign.

### P10 — Tenant Isolation & Security by Default
Tenant/property isolation is enforced at the data-access layer, not by convention. Role-based authorization on every API and capability. No secrets in source, configuration files, pipelines, or IaC state; secrets live in a secret store accessed via workload/managed identity. Least privilege for every identity. Audit records are append-only.

### P11 — Observable & Traceable
Structured logs, metrics, and distributed traces everywhere, exported through vendor-neutral OpenTelemetry. Correlation and causation IDs propagate across every event, capability, and inference call. Every AI decision is reconstructable from its trace (trigger, context, sources and versions, model, prompt version, capability calls, emitted events, response), with sensitive data redacted by policy. Failed processing is visible, never silent.

### P15 — Dependency Injection Through a Single Composition Root
*(Grouped with the architecture principles; principle IDs are stable and never renumbered.)*

Every dependency arrives as an interface (port) through **constructor injection**, wired by the platform's built-in container (`Microsoft.Extensions.DependencyInjection`).

- **Constructor injection only.** No property or method injection; no service locator — business code never asks `IServiceProvider` for services and never reads `IConfiguration` directly (settings arrive as `IOptions<T>` of an `*Options` class). A constructor needing more than about five dependencies signals a class doing too much.
- **Depend on ports, not implementations** (P5). `TimeProvider` is the accepted BCL exception.
- **Domain code knows nothing about DI:** no container packages or attributes; values such as IDs and the current time are passed in as arguments.
- **One composition root per host.** Only the host decides which implementation serves each port; adapter choice per operation/tenant goes through configuration bindings (P5), never hard-coded registrations.
- **Each module registers itself** through one public `Add<Module>(this IServiceCollection, …)` extension in its `DependencyInjection.cs`; implementation classes stay `internal`.
- **Deliberate lifetimes:** singleton for stateless or thread-safe services (adapters, resolvers, clock, ID generator); scoped for per-request or per-message state (unit of work, repositories, current actor and tenant); transient rarely. A singleton never captures a scoped service.
- **Fail fast:** every host validates the container on build (`ValidateOnBuild`, `ValidateScopes`) in every environment, and bindings at startup, so wiring errors stop the host before it serves traffic.
- **Tests don't need the container for units:** unit tests construct classes directly with fakes; integration tests use the real composition root and replace only what they need.

A third-party container is allowed only behind the same registrations (via `IServiceProviderFactory`), only if it is actively maintained for the current .NET release, and only through a recorded decision.

---

## 3. Delivery Principles

### P12 — Infrastructure as Code, Idempotent Deployments
- **Everything is code:** cloud resources, networking, identities, role assignments, secret *references*, app configuration (including port and inference bindings), and DB schema. No manual portal/CLI changes; anything manual is drift and is reverted or codified.
- **Idempotent by construction:** re-running any deployment on the same commit yields the same state and is a no-op — declarative IaC, immutable artifacts tagged by commit SHA (never `latest`), idempotent DB migrations, upsert-based seed data, check-then-act post-deploy steps.
- **Cloud-agnostic IaC mirroring the ports:** IaC modules are defined per infrastructure port with one implementation per cloud, so infrastructure can follow per-operation bindings (Terraform is the standard tool for this reason).
- **Build once, promote everywhere:** the same artifacts move through environments; only environment configuration differs.
- **Automated via GitHub Actions only:** OIDC workload identity federation (no long-lived cloud credentials), protected environments with approvals for production, concurrency control per environment, scheduled drift detection, rollback = redeploy a previous SHA through the same pipeline.
- **Protected state:** remote, locked, versioned IaC state; stateful resources protected against accidental destroy.

### P13 — Testable by Design
- Every port has a **local/in-memory adapter** for development and tests, and a **contract test suite** that every adapter (local or cloud) must pass.
- **Architecture tests** enforce P5 and P9 (layering, no vendor SDKs outside adapters) in CI.
- AI behavior is verified by **evaluation scenarios** runnable against a deterministic fake model and against any real bound model.
- Time (`TimeProvider`) and IDs are injected so behavior is deterministic in tests.

### P14 — Small, Green, Recorded Increments
Work proceeds in small vertical slices; a slice is done only when it builds and all tests pass. New infrastructure needs follow: **port → local adapter + contract tests → cloud adapter**. Tests accompany the slice they cover. Decisions and deviations are recorded in the phase plan's decision log.

---

## 4. Governance

### How phases use these principles
Each phase implementation plan (`phase-N-implementation-plan.md`) must:
1. Link to this document, [solution-structure.md](solution-structure.md), and [coding-standards.md](coding-standards.md) at the top.
2. Include a **Principles Conformance** section mapping each principle to how that phase realizes it (or "n/a this phase" with a reason).
3. List any **waivers**.

### Waivers
A waiver is a time-boxed, explicit exception to a principle. Each waiver records: principle ID, scope, reason, risk, and the phase/slice by which it will be removed. Waivers live in the phase plan's decision log. Silent deviations are defects.

### Changing principles
Principles change only by an explicit edit to this document with a change-log entry below. Phase plans then update their conformance sections.

## 5. Change Log

| Date | Change |
|---|---|
| 2026-10-23 | Initial principles P1–P14, extracted from Phase 1 product spec §4 and Phase 1 implementation planning. |
| 2026-10-23 | Solution structure and coding standards split into companion documents. |
| 2026-10-24 | Added P15 — Dependency Injection Through a Single Composition Root (built-in container; Unity rejected as unmaintained for current .NET). |
