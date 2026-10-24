# Hospitality AI OS — Phase 1 Product Specification
## Core Hospitality Platform + Event Backbone + AI Concierge

**Version:** 0.1  
**Audience:** Product, Engineering, Codex/Coding Agents, QA  
**Purpose:** Define the business requirements, user journeys, user stories, acceptance criteria, and launch boundaries for Phase 1 of the Hospitality AI OS.

---

# 1. Phase 1 Objective

Phase 1 establishes the architectural and product foundation for an autonomous hospitality operating system.

The system must:

1. Understand a property and its guest-facing capabilities.
2. Answer guest questions accurately using authoritative property data.
3. Represent hotel operations through canonical domain objects.
4. Emit and consume canonical domain events.
5. Execute digital actions through a capability layer rather than direct database or vendor calls.
6. Support autonomous event-driven processing.
7. Create `HumanAction` work items whenever physical-world work is required.
8. Provide staff with a simple surface to receive and complete physical tasks.
9. Resume autonomous processing when the human action is completed.
10. Abstract all external systems behind platform-owned integration interfaces.

Phase 1 is not intended to operate the entire hotel.

It is intended to prove the core operating model:

```text
Event
  ↓
Autonomous component evaluates state
  ↓
Decision
  ↓
Digital capability OR HumanAction
  ↓
Resulting event
  ↓
Autonomous processing continues
```

---

# 2. Phase 1 Customer Outcome

At the end of Phase 1, a hotel must be able to:

- Configure a property.
- Configure rooms, amenities, restaurants, menus, services, policies, and events.
- Publish a guest-facing AI concierge.
- Allow guests to ask questions about the property.
- Receive accurate answers grounded in authoritative property data.
- Allow guests to make basic physical-service requests such as towels or pillows.
- Convert those requests into autonomous workflows.
- Route physical work to a staff member.
- Allow staff to complete the physical task.
- Emit completion events.
- Allow the AI workflow to resume and notify the guest automatically.
- View traces and audit history of AI decisions and actions.
- Run entirely on mock or native integrations while preserving future external-system abstraction.

---

# 3. Product Scope

## In Scope

### Property Configuration
- Organization
- Property
- Spaces
- Accommodation types and units
- Restaurants
- Menus and menu items
- Amenities
- Services
- Events
- Operating hours
- Policies
- Property content

### Guest Experience
- Property Q&A
- Amenity discovery
- Dining discovery
- Menu discovery
- Event/activity discovery
- Policy questions
- Directions/location relationships
- Basic guest service requests
- Service request status

### Autonomous Operations Foundation
- Event bus
- Canonical event envelope
- Event handlers
- Capability registry
- Autonomous controllers
- `HumanAction`
- Staff task surface
- Task completion events
- Workflow continuation
- Audit trail

### AI Platform
- AI Gateway
- Model abstraction
- Prompt registry
- Context builder
- Tool/capability invocation
- Structured output validation
- Traceability
- Evaluation scenarios

### Integration Foundation
- Canonical integration interfaces
- Provider registry
- Provider resolver
- Mock/native connectors
- `ExternalReference`

## Out of Scope

Do not build beyond interfaces/placeholders unless needed for a Phase 1 journey:

- Full PMS replacement
- Real CRS/OTA integrations
- Full POS integration
- Payments/folio settlement
- Check-in/checkout
- Revenue management
- Loyalty
- Marketplace
- Complex reservation booking
- Dynamic packaging
- Portfolio analytics
- Fully autonomous housekeeping optimization
- Complex maintenance dispatch
- Full restaurant ordering
- Full workforce optimization
- Native mobile apps
- IoT/device integrations
- AI-assisted property onboarding from a website or content documents. In Phase 1, property data is entered manually through the admin UI/API. Specified as a Phase 2 candidate: see [phase-2-product-spec-hospitality-ai-os.md §2](phase-2-product-spec-hospitality-ai-os.md).

---

# 4. Non-Negotiable Architecture Principles

Architecture principles are defined once, for all phases, in [architecture-principles.md](architecture-principles.md). Phase 1 conforms to all of them (P1–P14).

The principles originally listed here map as follows:

| Former section | Principle |
|---|---|
| 4.1 AI-First, Event-Driven | P1 |
| 4.2 Humans Are Physical-World Executors | P2 |
| 4.3 No Direct Vendor Dependencies | P5 |
| 4.4 No Direct LLM Database Mutation | P7 |
| 4.5 Canonical Domain Ownership | P3 |
| 4.6 Idempotency by Default | P8 |

---

# 5. Phase 1 Personas

## Guest
Uses the guest web experience to learn about the property, discover services, create physical-service requests, and check status.

## Property Administrator
Maintains property information, menus, amenities, services, events, operating hours, policies, and content.

## Physical Task Worker
Receives clear physical actions, accepts/starts/completes work, and returns completion/failure state to the autonomous system.

## Property Manager / Operator
Observes AI activity, open HumanActions, operational exceptions, traces, and audit history. Managers are not required for normal workflow progression.

---

# 6. Minimum Canonical Domain Model

```text
Organization
Property
Space
AccommodationType
AccommodationUnit
Guest
StayContext
Restaurant
Menu
MenuItem
Amenity
Service
PropertyEvent
OperatingHours
Policy
ContentItem
Conversation
GuestMessage
ServiceRequest
HumanAction
TaskAssignment
CapabilityDefinition
CapabilityInvocation
DomainEvent
ExternalReference
IntegrationProvider
IntegrationConfiguration
AITrace
AuditRecord
```

---

# 7. HumanAction

`HumanAction` is a first-class Phase 1 entity representing a physical-world action that AI cannot directly execute.

Required fields:

```text
HumanAction

id
propertyId
requestedByAgent
sourceEventId
reason

actionType
instructions
location

priority
deadline
sla

skillsRequired
assignedTo
status

createdAt
acceptedAt
startedAt
completedAt

completionData
evidence
result
```

Minimum statuses:

```text
Requested
Assigned
Accepted
InProgress
Completed
Failed
Cancelled
Expired
```

Minimum action types:

```text
DELIVER_ITEMS
CLEAN_ROOM
INSPECT_ROOM
CHECK_ROOM
REPAIR_EQUIPMENT
RESTOCK_ITEM
OTHER
```

---

# 8. Canonical Event Envelope

Every event must use one common envelope:

```text
DomainEvent

eventId
eventType
eventVersion

tenantId
organizationId
propertyId

aggregateType
aggregateId

occurredAt
recordedAt

correlationId
causationId

actorType
actorId

payload
metadata
```

Required characteristics:
- Globally unique event ID
- Versioned schema
- Correlation propagation
- Causation tracking
- Idempotent processing
- Replay support
- Auditability

Minimum Phase 1 events:

```text
PropertyCreated
PropertyUpdated
AmenityCreated
AmenityUpdated
RestaurantCreated
RestaurantUpdated
MenuUpdated
PolicyCreated
PolicyUpdated
PropertyEventCreated
PropertyEventUpdated
GuestMessageReceived
GuestQuestionAnswered
ServiceRequestCreated
ServiceRequestUpdated
ServiceRequestCompleted
HumanActionRequested
HumanActionAssigned
HumanActionAccepted
HumanActionStarted
HumanActionCompleted
HumanActionFailed
CapabilityInvocationRequested
CapabilityInvocationSucceeded
CapabilityInvocationFailed
GuestNotificationRequested
GuestNotificationSent
AITraceCreated
```

---

# 9. Autonomous Controllers

## Concierge Controller
- Handle guest questions
- Build relevant context
- Use structured/property graph/document sources appropriately
- Answer only from authoritative information
- Prefer explicit uncertainty over fabrication

## Guest Service Controller
- Interpret physical-service requests
- Create `ServiceRequest`
- Determine whether a `HumanAction` is required
- Create HumanAction
- Monitor result events
- Complete/advance workflow autonomously
- Notify guest

## HumanAction Router
- Route HumanAction to an eligible worker or queue
- Track assignment
- Support reassignment
- Emit assignment events

Phase 1 may use deterministic rule-based routing. Workforce optimization is deferred.

---

# 10. Integration Contracts

Define these interfaces even if implementations are mock/native:

```text
IPmsConnector
ICmsConnector
IPosConnector
IMessagingConnector
ICrsConnector        // placeholder allowed
IPaymentConnector    // placeholder allowed
```

Initial implementations:

```text
MockPmsConnector
NativeCmsConnector
MockPosConnector
MockMessagingConnector
```

No domain code may reference concrete connector implementations.

---

# 11. Capability Registry

Minimum Phase 1 capabilities:

```text
Property.GetProperty
Property.GetOperatingHours
Property.GetPolicy
Property.SearchContent
Dining.SearchRestaurants
Dining.GetMenu
Events.SearchPropertyEvents
GuestService.CreateRequest
GuestService.GetRequestStatus
HumanAction.Create
HumanAction.Assign
HumanAction.UpdateStatus
HumanAction.Complete
Messaging.NotifyGuest
```

Every capability definition must contain:

```text
name
version
description
inputSchema
outputSchema
authorization
confirmationPolicy
sideEffects
idempotency
auditClassification
```

---

# 12. User Journey 1 — Property Administrator Creates a Property

## Business Requirement
A property administrator can create and configure a hotel without modifying source code.

## Story
As a Property Administrator, I want to create a hotel property and its base information so the platform can reason about and serve the property.

## Acceptance Criteria
- Given valid required fields, creation produces a canonical `Property` with platform-owned `propertyId`.
- `PropertyCreated` is emitted.
- Missing required data returns explicit validation errors and does not persist an invalid property.
- Creation is audited with actor, action, timestamp, and property ID.
- Tenant isolation prevents another organization from reading/modifying the property.

---

# 13. User Journey 2 — Configure Amenities and Operating Hours

## Business Requirement
Admins can configure amenities and accurate operating hours.

## Acceptance Criteria
- Amenity creation links it to the correct property and emits `AmenityCreated`.
- Operating hours are queryable through canonical APIs.
- Updates emit `AmenityUpdated` and future guest answers use the new state.
- Hours support closed days, multiple intervals per day, temporary closures, and date overrides.
- Guest-facing answers are traceable to the exact structured record/version used.

---

# 14. User Journey 3 — Configure Restaurants and Menus

## Acceptance Criteria
- Admin can create restaurant name, cuisine, location, hours, and description.
- Admin can create menus and menu items.
- Menu items support name, description, price, category, dietary attributes, and availability flag.
- Guest searches can filter by dietary metadata.
- Canonical entities contain no POS-vendor-specific fields.

---

# 15. User Journey 4 — Configure Policies

## Business Requirement
Policy data is represented separately from marketing content.

## Acceptance Criteria
- Admin can create/update policy type, title, content, effective date, property, and status.
- AI distinguishes policy from descriptive content.
- Missing policy causes an explicit unknown/uncertain answer rather than fabrication.
- Policy updates emit `PolicyUpdated`.

---

# 16. User Journey 5 — Guest Asks a Property Question

Example: `What time does the pool close tonight?`

## Acceptance Criteria
- AI selects structured operating-hours data when it exists.
- Response is grounded only in authoritative property context.
- Trace records guest message, context retrieved, model, capability/tool calls, authoritative source, and response.
- Missing information produces an explicit unconfirmed/unknown response.
- `GuestMessageReceived` occurs before processing and `GuestQuestionAnswered` occurs after response creation.

---

# 17. User Journey 6 — Guest Asks a Policy Question

Example: `Can my 12-year-old use the gym?`

## Acceptance Criteria
- If a matching policy exists, answer according to that policy.
- AI may summarize but not materially change policy meaning.
- Ambiguous policy produces explicit uncertainty.
- Missing policy must not result in an invented age rule.
- Scenario is included in automated AI evaluations.

---

# 18. User Journey 7 — Guest Discovers Dining

Example: `What vegetarian food can I get here?`

## Acceptance Criteria
- AI searches restaurants represented in canonical data.
- AI uses dietary metadata to find matching menu items.
- It does not claim real-time availability unless canonical availability data exists.
- Prices come only from canonical menu data.
- Phase 1 does not place orders.

---

# 19. User Journey 8 — Guest Discovers Events/Activities

Example: `What can we do tonight?`

## Acceptance Criteria
- Events can be queried by property, time, category, and audience.
- Past events are not returned as future options.
- Property-local time is used.
- If booking is unsupported, AI must not imply a reservation was made.

---

# 20. User Journey 9 — Guest Requests Towels

Example: `Can you send two towels to my room?`

## Business Requirement
A natural-language physical-service request becomes an autonomous operational workflow.

## Required Flow

```text
GuestMessageReceived
  ↓
Guest Service Controller
  ↓
Intent = deliver towels
  ↓
GuestService.CreateRequest
  ↓
ServiceRequestCreated
  ↓
HumanAction.Create
  ↓
HumanActionRequested
  ↓
HumanAction Router
  ↓
HumanActionAssigned
  ↓
Staff performs work
  ↓
HumanActionCompleted
  ↓
Guest Service Controller resumes
  ↓
ServiceRequestCompleted
  ↓
GuestNotificationSent
```

## Acceptance Criteria
- System extracts service type, item, quantity, and location.
- Canonical `ServiceRequest` is created.
- HumanAction includes delivery instructions, room/location, item/quantity, priority, and SLA.
- No manager approval is required.
- HumanAction appears on the appropriate staff surface.
- Worker completion emits `HumanActionCompleted`.
- Guest Service Controller consumes completion and completes the ServiceRequest autonomously.
- Guest is notified automatically.
- Workflow events share a correlation ID.
- Duplicate processing must not create duplicate deliveries.

---

# 21. User Journey 10 — Guest Requests a Pillow

Example: `We need another pillow.`

## Acceptance Criteria
- Generalized delivery workflow is reused.
- Extracted item is `pillow`, quantity `1` unless the guest specifies otherwise.
- No separate pillow-specific workflow implementation is permitted.

---

# 22. User Journey 11 — Guest Reports a Maintenance Problem

Example: `The AC isn't working.`

## Acceptance Criteria
- System recognizes maintenance intent.
- Creates a `ServiceRequest` with type `MAINTENANCE` and appropriate category when confidence is sufficient.
- Creates `HumanAction` such as `REPAIR_EQUIPMENT` or `CHECK_ROOM`.
- Guest receives acknowledgement automatically.
- Worker can start and complete the task.
- Completion emits event and autonomous controller resumes.
- Guest is notified after completion.
- Failed/unresolved work keeps the ServiceRequest open and records an exception.
- Advanced replanning is deferred to later phases.

---

# 23. User Journey 12 — Guest Checks Request Status

Example: `Where are my towels?`

## Acceptance Criteria
- Active request is identified from guest/stay context.
- AI reports canonical status only: Requested, Assigned, InProgress, Completed, etc.
- AI does not invent an ETA.
- Response is grounded in `ServiceRequest` and `HumanAction` state.

---

# 24. User Journey 13 — Worker Views Assigned HumanActions

## Acceptance Criteria
- Worker sees only authorized actions.
- Every action shows location, instructions, priority, deadline/SLA, and status.
- Worker can Accept, Start, Complete, or Fail.
- Each state change emits the corresponding event.
- Worker UI hides internal orchestration complexity.

---

# 25. User Journey 14 — Worker Completes Physical Work

## Acceptance Criteria
- Worker can complete HumanAction.
- Completion records worker, timestamp, optional note, and result.
- Logical completion occurs exactly once.
- Duplicate completion requests are idempotent.
- Parent autonomous workflow is resumed by the resulting event.

---

# 26. User Journey 15 — Manager Inspects AI Trace

## Acceptance Criteria
Trace includes:
- Triggering message/event
- Interpreted intent
- Retrieved context
- Model/provider
- Prompt/version
- Capability calls/results
- Authorization/policy result
- Emitted events
- Final response/action

Sensitive data is redacted according to policy.

---

# 27. User Journey 16 — Admin Changes Operating Hours

## Acceptance Criteria
- Admin updates operating hours.
- Correct domain event is emitted.
- New guest answers use updated hours immediately or within documented cache SLA.
- Old AI traces remain tied to the version of data used at that time.

---

# 28. User Journey 17 — Unknown Guest Question

Example: `Does the hotel provide free helicopter transfers?`

Assume no authoritative data exists.

## Acceptance Criteria
- AI does not invent an answer.
- AI explicitly states that confirmed information is unavailable.
- Question/outcome is recorded for future content improvement.
- Trace shows that no authoritative source was found.

---

# 29. User Journey 18 — Integration Provider Is Swapped

Scenario: change provider configuration from `MockPmsConnector` to a future real PMS implementation.

## Acceptance Criteria
- Domain services require no code changes.
- AI capabilities require no code changes.
- Only provider configuration/connector implementation changes.
- Canonical IDs remain unchanged.
- Vendor/external IDs remain in `ExternalReference`.

---

# 30. User Journey 19 — Duplicate Event Delivery

## Acceptance Criteria
Given the same `HumanActionCompleted` event is delivered more than once:
- Handler is safe under duplicate delivery.
- ServiceRequest is completed once logically.
- Guest receives one logical completion notification.
- Duplicate processing is observable but harmless.

---

# 31. User Journey 20 — Failed HumanAction

Example: worker cannot access the room.

## Acceptance Criteria
- Worker can fail the HumanAction and provide a reason.
- `HumanActionFailed` is emitted.
- Parent ServiceRequest remains unresolved.
- Autonomous controller records an exception state.
- Phase 1 may route exception to a manager queue.
- Advanced autonomous replanning is deferred.

---

# 32. Staff Surface Requirements

Primary views:

```text
My Actions
Unassigned Actions
Completed Actions
Exceptions
```

Example action card:

```text
Room 412
Deliver:
- 2 bath towels

Priority: Normal
Requested: 8 minutes ago
Target: 10:42 AM

[ACCEPT]
```

Then:

```text
[START]
```

Then:

```text
[COMPLETE] [FAIL]
```

---

# 33. Guest Surface Requirements

One conversational surface supports:
- Property questions
- Amenity/policy questions
- Dining discovery
- Event discovery
- Service requests
- Service status

The guest must not need to understand internal modules or departments.

---

# 34. Admin Surface Requirements

Minimum sections:

```text
Property
Spaces
Amenities
Restaurants
Menus
Events
Services
Policies
Operating Hours
Content
Integrations
AI Traces
```

---

# 35. AI Safety and Correctness Requirements

Required:
- Structured output validation
- Capability allow-list
- No raw SQL mutation tools for agents
- No arbitrary HTTP calls from agents
- No direct vendor API calls from agents
- Source-grounded property answers
- Confirmation only where policy explicitly requires it
- Audit of every capability invocation
- Prompt version tracking
- Model version tracking

---

# 36. AI Evaluation Requirements

Automated evaluations must include at least:

```text
1. Pool closing time
2. Gym child age policy
3. Unknown policy
4. Vegetarian dining discovery
5. Events tonight
6. Request two towels
7. Request one pillow
8. Report AC problem
9. Request status
10. Duplicate request protection
11. Unknown factual question
12. Updated operating hours
```

Each evaluation defines:
- Input
- Required context
- Expected behavior
- Disallowed behavior
- Expected capability calls
- Expected events

---

# 37. Non-Functional Requirements

## Reliability
- Event handlers idempotent
- Capability invocations idempotent where applicable
- Retry transient failures
- Dead-letter permanent failures
- Correlation IDs propagated
- Failed workflows observable

## Security
- Staff/admin authentication
- Guest context handled securely
- Tenant/property isolation
- Role-based access
- Secrets external to source code
- Append-only/immutable audit behavior

## Observability
- Structured logs
- Metrics
- Distributed traces
- AI traces
- Event processing metrics
- Capability success/failure metrics

## Performance Targets
- Standard property query API p95 < 500 ms excluding LLM time
- Interactive AI response latency suitable for conversational use
- Staff HumanAction updates visible within seconds
- Event propagation near real-time

---

# 38. API Categories

```text
/api/v1/organizations
/api/v1/properties
/api/v1/spaces
/api/v1/amenities
/api/v1/restaurants
/api/v1/menus
/api/v1/events
/api/v1/services
/api/v1/policies
/api/v1/content
/api/v1/conversations
/api/v1/service-requests
/api/v1/human-actions
/api/v1/capabilities
/api/v1/integrations
/api/v1/ai/traces
```

---

# 39. Recommended Repository Structure

```text
hospitality-os/

  apps/
    guest-web/
    staff-web/
    admin-web/

  platform/
    api/
    ai/
    capabilities/
    workflows/
    authorization/

  domains/
    organizations/
    properties/
    spaces/
    dining/
    events/
    policies/
    services/
    operations/

  integration-contracts/
    pms/
    cms/
    pos/
    messaging/
    crs/
    payments/

  integrations/
    pms/mock/
    cms/native/
    pos/mock/
    messaging/mock/

  integration-runtime/
    provider-registry/
    provider-resolver/
    external-references/
    health/

  infrastructure/
    persistence/
    events/
    identity/
    observability/
    search/

  contracts/
    api/
    events/
    capabilities/

  testing/
    unit/
    integration/
    end-to-end/
    ai-evals/

  dev/
    seed-data/
    local-environment/
```

---

# 40. Build Epics

## Epic 1 — Solution Foundation
Build .NET solution, module boundaries, configuration, health checks, local environment, CI.

**Acceptance:** whole solution builds; dependency boundaries are enforced/testable; CI runs tests.

## Epic 2 — Canonical Domain Core
Build IDs, entities, Property, Space, Amenity, Restaurant, Menu, Event, Policy, ServiceRequest, HumanAction.

**Acceptance:** no vendor-specific models in domain projects; invariants covered by tests.

## Epic 3 — Event Backbone
Build event envelope, publisher/subscriber abstractions, idempotency, correlation/causation, retries, dead-letter abstraction.

**Acceptance:** events publish/handle successfully; duplicates are safe; correlation chain preserved.

## Epic 4 — Capability Registry
Build capability definitions/invocations, registry, executor, validation, authorization hooks, audit hooks.

**Acceptance:** agents can execute only registered capabilities; unknown capability rejected; every invocation audited.

## Epic 5 — Integration Abstraction
Build connector contracts, provider registry/resolver, mock connectors, ExternalReference.

**Acceptance:** provider can be swapped without domain changes; domain code has no concrete connector dependency.

## Epic 6 — Property Admin
Build property/admin UI for amenities, menus, policies, hours, events, services.

**Acceptance:** Aurora Grand reference property can be configured entirely through UI/API.

## Epic 7 — AI Gateway + Context Engine
Build model abstraction, prompt registry, context builder, grounding, structured response pipeline, trace capture.

**Acceptance:** a property question works end-to-end and trace identifies its authoritative source.

## Epic 8 — AI Concierge
Build guest chat, Concierge Controller, property/policy Q&A, dining and event discovery.

**Acceptance:** defined AI evaluation scenarios pass.

## Epic 9 — Guest Service Controller
Build service-request intent handling, ServiceRequest, HumanAction creation, router, autonomous continuation.

**Acceptance:** towel request completes end-to-end.

## Epic 10 — Staff HumanAction Surface
Build assigned queue, accept/start/complete/fail, exception view.

**Acceptance:** worker completion triggers autonomous continuation.

## Epic 11 — Messaging / Guest Notification
Build `IMessagingConnector`, mock messaging, guest notifications.

**Acceptance:** guest automatically receives service completion message.

## Epic 12 — Observability + AI Evaluations
Build logs, metrics, traces, AI trace UI/data, evaluation harness.

**Acceptance:** every Phase 1 journey has automated or semi-automated acceptance coverage.

---

# 41. Recommended Codex Implementation Order

```text
1. Repository + solution boundaries
2. Canonical IDs + domain entities
3. Event envelope + event bus abstraction
4. Capability Registry
5. Integration contracts + provider resolver
6. Persistence
7. Property APIs
8. Admin UI
9. AI Gateway
10. Context Engine
11. Concierge Controller
12. Guest web
13. ServiceRequest
14. HumanAction
15. HumanAction Router
16. Staff web
17. Autonomous continuation handlers
18. Messaging
19. AI evaluations
20. End-to-end acceptance tests
```

---

# 42. Phase 1 Definition of Done

Phase 1 is complete only when all of the following are true.

## Property
- Aurora Grand reference hotel is fully configured.
- Amenities, dining, events, services, policies, and hours exist.

## Guest AI
- Guest can ask property questions.
- Answers are grounded.
- Unknown information is not invented.
- Dining and event discovery work.

## Autonomous Operations
- Guest can request towels.
- Guest can request a pillow.
- Guest can report an AC issue.
- Each creates canonical operational state.
- Physical work becomes HumanAction.
- Worker receives HumanAction.
- Worker completes HumanAction.
- Completion emits event.
- AI workflow resumes automatically.
- Guest is notified automatically.

## Events
- Event envelope implemented.
- Idempotency implemented.
- Correlation/causation implemented.
- Retry/dead-letter/replay concepts implemented.

## Integrations
- External systems abstracted.
- Provider resolver implemented.
- Mock/native providers work.
- No vendor schema leaks into domain.

## AI
- AI Gateway implemented.
- Context engine implemented.
- Capability execution implemented.
- AI traces implemented.
- Required evaluations pass.

## Security
- Tenant isolation exists.
- Staff/admin authentication exists.
- Authorization hooks exist.
- Audit trail exists.

## Operations
- Logs exist.
- Metrics exist.
- Traces exist.
- Failed event processing is visible.

---

# 43. Phase 1 Demo Script

## Demo 1 — Property Q&A
Guest: `What time does the pool close tonight?`

AI answers from canonical operating hours.

## Demo 2 — Policy
Guest: `Can my 12-year-old use the gym?`

AI answers from canonical policy.

## Demo 3 — Dining
Guest: `What vegetarian food can I get here?`

AI searches canonical menus.

## Demo 4 — Events
Guest: `What's happening tonight?`

AI searches property events.

## Demo 5 — Autonomous Physical Workflow
Guest: `Send two towels to my room.`

System autonomously:

```text
interprets request
creates ServiceRequest
creates HumanAction
routes HumanAction
```

Staff sees:

```text
Room 412
Deliver 2 bath towels
```

Staff completes it.

System autonomously:

```text
consumes HumanActionCompleted
completes ServiceRequest
notifies guest
```

No manager takes an action.

---

# 44. Product Principle to Preserve

The system must always evolve toward:

```text
Sense
  ↓
Understand
  ↓
Decide
  ↓
Act
  ↓
Observe
  ↓
Adapt
  └──────→ repeat
```

Humans are not the workflow engine.

Humans are participants in the physical world.

The AI Hospitality OS is the operating system.
