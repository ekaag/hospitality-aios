# Phase 1 Definition-of-Done Audit (L3)

Audited 2026-11-02 on branch `l3-definition-of-done`, against [the product spec §42](phase-1-product-spec-hospitality-ai-os.md)
and the [architecture principles](architecture-principles.md) P1–P15. Each item is **Met** (with the evidence that proves
it, all in CI unless noted), **Fixed in L3**, or **Open**. "Verify on first deploy" lists what only a real Azure deployment
can show; everything else is proven locally and in CI.

Test names are given as `Class.Method`; integration tests are in `tests/HospitalityOS.IntegrationTests`, the demo in
`apps/e2e`.

## Spec §42

### Property

| Item | Status | Evidence |
|---|---|---|
| Aurora Grand reference hotel is fully configured | Met | `AuroraGrandSeedTests` (Demos 1–5 data, idempotent re-seed, audited as the seed); the local launch profile and the migration job (dev, test) seed it |
| Amenities, dining, events, services, policies, and hours exist | Met | `AuroraGrandSeedTests.Demo1_ThePoolClosesAt10Tonight`, `Demo2_…`, `Demo3_…` (both restaurants' menus), `Demo4_…`, `Demo5_StaffSeeTheServicesForTowelsPillowsAndRepairs`; admin-web sections for each |

### Guest AI

| Item | Status | Evidence |
|---|---|---|
| Guest can ask property questions | Met | `ConciergeControllerTests`, `ConciergeJourneyTests`; browser: `demo.spec.ts` Demos 1–4 |
| Answers are grounded | Met | `GroundedAnsweringHostTests` (an answer citing an unretrieved record becomes the honest no-source answer); `AITraceApiTests.PoolQuestion_TheTraceNamesTheOperatingHoursRecordAndVersion…` |
| Unknown information is not invented | Met | `ConciergeControllerTests.AQuestionNoSourceAnswers_GetsTheHonestAnswer_NeverTheInventedOne`, `ConciergeJourneyTests.WithoutAPolicyInEffect_NoRuleIsInvented`, evals 3 and 11 |
| Dining and event discovery work | Met | `ConciergeJourneyTests.VegetarianDining_…`, `TonightsEvents_AreInPropertyLocalTime_…`, `ABookableEvent_SaysBookingIsRequired_…`; evals 4 and 5 |

### Autonomous Operations

| Item | Status | Evidence |
|---|---|---|
| Guest can request towels | Met | `GuestServiceControllerTests.TwoTowels_BecomeOneRequestForRoom412_…`; eval 6; browser Demo 5 |
| Guest can request a pillow | Met | `GuestServiceControllerTests.APillow_IsOneUnlessTheGuestSaysOtherwise_…`; eval 7 |
| Guest can report an AC issue | Met | `GuestServiceControllerTests.ABrokenAirConditioner_BecomesAMaintenanceRequest…`; eval 8 |
| Each creates canonical operational state | Met | the same tests read the `ServiceRequest` and its `HumanAction` from the repositories |
| Physical work becomes HumanAction | Met | `GuestServiceControllerTests.TheTowelJourney_…`, `HumanActionCapabilitiesTests` |
| Worker receives HumanAction | Met | `HumanActionRouterTests` (least loaded with the skill, on duty, reassigned if not accepted in time); `RealtimeHubTests`; browser Demo 5 |
| Worker completes HumanAction | Met | `WorkEndpointsTests`, `WorkEvidenceTests`; browser Demo 5 (accept, start, complete) |
| Completion emits event | Met | `RequestContinuationTests.WorkStartedThenCompleted_MovesTheRequestOn_…` (`HumanActionCompleted` in the request's flow) |
| AI workflow resumes automatically | Met | `RequestContinuationTests` (the request completes on the event, once even if delivered twice; failed and expired work go to exception) |
| Guest is notified automatically | Met | `GuestServiceControllerTests.TheTowelJourney_…AndTellsTheGuestOnce`; browser Demo 5 ("delivered to Room 412", with no manager signed in) |

### Events

| Item | Status | Evidence |
|---|---|---|
| Event envelope implemented | Met | `EventFlowTests.Commit_AggregateChange_IsPublishedToSubscribersAsCanonicalEnvelope`; every payload matches its committed JSON sample |
| Idempotency implemented | Met | `EventHandlingTests` (redelivery, replay, a race past the inbox check: effect once); capability idempotency keys in `CapabilityExecutorTests` |
| Correlation/causation implemented | Met | `EventHandlingTests.Handler_CommittedEvent_TakesEffectOnceAndContinuesTheFlow`, `GuestServiceControllerTests.TheTowelJourney_…SharesOneCorrelationId…`, `CorrelationMiddlewareTests` |
| Retry/dead-letter/replay concepts implemented | Met | outbox retry and dead letter (`OutboxDispatcherTests`), handler redelivery and dead letter (`EventHandlingTests`, transport contract suites incl. the Service Bus emulator), `IEventReplayer`, and retry from the failed-event view (`FailedEventEndpointsTests`) |

### Integrations

| Item | Status | Evidence |
|---|---|---|
| External systems abstracted | Met | `IntegrationContractTests.Connectors_AreExactlyTheSpecConnectors`, `…Interfaces_AreTheConnectorsAndTheProviderSeam` |
| Provider resolver implemented | Met | `IntegrationRuntimeTests` (configure, resolve, swap provider by configuration, disable, health) |
| Mock/native providers work | Met | contract suites for `MockPms`, `NativeCms`, `MockPos`, `MockMessaging`; the seed's PMS stay import |
| No vendor schema leaks into domain | Met | `IntegrationContractTests.PublicSignatures_UseOnlyCanonicalTypes`; external IDs only in `ExternalReference` (P3) |

### AI

| Item | Status | Evidence |
|---|---|---|
| AI Gateway implemented | Met | the inference router (tasks, fallbacks, per-tenant overrides): `InferenceAdapterRoutingTests`, `AITraceApiTests.ConciergeAnswer_MovesToAnotherProviderByConfigurationOnly…`; adapters Fake, Anthropic, Azure OpenAI |
| Context engine implemented | Met | grounding over structured sources, then search and vectors: `GroundedAnsweringHostTests`, `ReadCapabilitiesTests`, `ContentSearchOutageTests` |
| Capability execution implemented | Met | `CapabilityExecutorTests` (lookup, authorization, confirmation, idempotency, schemas, atomic save, audit), `ToolLoopHostTests` |
| AI traces implemented | Met | `AITraceApiTests` (sources and versions, redaction by role, tenant only, every provider tried) |
| Required evaluations pass | Met (Fake model in CI) | `HospitalityOS.AiEvals`: all 12 spec §36 scenarios; against real models: verify on first deploy (`ai-evals-live.yml`) |

### Security

| Item | Status | Evidence |
|---|---|---|
| Tenant isolation exists | Met | EF Core query filter on every aggregate; only two documented cross-tenant lookups by unique key (`EfStaffAccessDirectory`, `EfGuestLinkDirectory`); 24 test files assert another tenant sees nothing |
| Staff/admin authentication exists | Met | Entra External ID sign-in with invites (`AccessEndpointsTests`), stay tokens for guests (`StayEndpointsTests`), dev tokens in Development only |
| Authorization hooks exist | **Fixed in L3** | new `EndpointAuthorizationTests`: every endpoint names a policy or roles, except an explicit list meant for anyone. It found the capability catalog open to anyone; now managers and administrators only |
| Audit trail exists | Met | `AuditLogContractTests` (append-only), audit entries for capabilities, administration, sign-in invites, retries of failed events |

### Operations

| Item | Status | Evidence |
|---|---|---|
| Logs exist | Met | structured `ILogger` logging throughout, exported with OpenTelemetry (`TelemetryStartupTests`) |
| Metrics exist | Met | metric collectors in `EventHandlingTests.Processing_IsMeasured_…`, `CapabilityExecutorTests.Execute_IsMeasured_…`, `InferenceAdapterRoutingTests` (L1) |
| Traces exist | Met | `TelemetryStartupTests.Requests_AreTraced_ButHealthProbesAreNot`; spans for handled events and capability calls |
| Failed event processing is visible | Met | `FailedEventEndpointsTests`, admin-web Failed events |

## Principles

| Principle | Status | Evidence / note |
|---|---|---|
| P1 AI-first, event-driven | Met | every aggregate change raises an event (persistence contract suites; a sample required for every event type) |
| P2 Humans are physical-world executors | Met | `HumanAction` for physical work; browser Demo 5 completes with no manager |
| P3 Canonical domain ownership | Met | `ExternalReference` only; `IntegrationContractTests` |
| P4 Grounded, honest AI | Met | grounding refusal tests; "never quote `DueAt` as an ETA" (B9) |
| P5 Everything behind an interface | Met | `TypeDependencyTests`, `PackageReferenceTests`, `AdapterVisibilityTests` |
| P6 Inference as tasks | Met | tasks only in code; traces record provider, model, prompt version, usage |
| P7 Capabilities only | Met | `ToolLoopHostTests`, `CapabilityExecutorTests` (unknown capabilities refused) |
| P8 Idempotency by default | Met | handlers, capabilities, migrations (`MigrationModeTests`), deployments (orchestrator tests) |
| P9 Strict dependency direction | Met | `ProjectReferenceTests`, `TypeDependencyTests` |
| P10 Tenant isolation & security | **Fixed in L3** | authorization on every endpoint now enforced by a test (see Security); no secrets in source (scanned: only test placeholders and CI's throwaway database password) |
| P11 Observable & traceable | Met | OpenTelemetry, correlation and causation everywhere, AI traces, failed-event view |
| P12 IaC, idempotent deployments | **Fixed in L3** | drift detection now covers dev, test, and prod (an environment not yet deployed passes); `deploy-dev` can redeploy an earlier commit (rollback = redeploy a previous SHA). Deployment itself: verify on first deploy |
| P13 Testable by design | Met | contract suites for every port, architecture tests, evals on Fake and real models, P13 breaks recorded per slice |
| P14 Small, green, recorded increments | Met | plan §10 and §8 record every slice |
| P15 DI through one composition root | Met, noted | `DependencyInjectionTests`, `ValidateOnBuild`; noted: eight classes take 8–10 dependencies (largest `GuestServiceController`, 10), past P15's "about five" signal; left as they are for Phase 1 (each is one workflow step reading several repositories), a candidate for splitting in Phase 2 |

## Open

| Item | Why |
|---|---|
| K0: `/health` deploys to dev on merge, a re-run on the same SHA is a no-op, drift reports clean | Needs the Azure bootstrap (`deploy bootstrap --github`) and `bootstrap-signin` per environment, which only the subscription's owner can run |
| Performance targets (spec §37: property query p95 < 500 ms) | Meaningful only on deployed infrastructure; measure on the first deployment with the new request metrics |
| Web apps hosted in Azure | Not in the Phase 1 plan (catalog: planned "later"); until then the browser demo runs locally and in CI, and deployed check-in links give the code only |

## Verify on first deploy

Each runs by automation once the bootstrap is done.

| What | How |
|---|---|
| Dev deploys and stays clean | merge to `main` (or run `deploy-dev`), then `drift-detection` |
| Re-running changes nothing | run `deploy-dev` again on the same SHA, then `drift-detection` |
| Entra External ID sign-in (preview `ciamDirectories` type) | `bootstrap-signin --env dev`, then sign in to staff-web through it |
| PostgreSQL role creation (`pgaadauth`) and the migration job | the `migrate` job's run in `deploy.yml`; its output in `ContainerAppConsoleLogs_CL` |
| Telemetry arrives in Application Insights | query `requests` and `customMetrics` for `hospitalityos-api` |
| Azure OpenAI quota and models | `ai-evals-live.yml` on dev |
| Claude through the vault's key | add `ANTHROPIC_API_KEY`, redeploy, `ai-evals-live.yml` |
| Promotion with approval | `promote.yml` |
