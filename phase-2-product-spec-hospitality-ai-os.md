# Hospitality AI OS — Phase 2 Product Specification
## Candidate Features

**Version:** 0.1 (draft — candidate features, not yet committed scope)
**Audience:** Product, Engineering, Codex/Coding Agents, QA
**Governing principles:** [architecture-principles.md](architecture-principles.md) · [solution-structure.md](solution-structure.md) · [coding-standards.md](coding-standards.md)
**Builds on:** [phase-1-product-spec-hospitality-ai-os.md](phase-1-product-spec-hospitality-ai-os.md)

---

# 1. Purpose

This document collects features identified during Phase 1 that are deferred to Phase 2. Each feature is specified to the same standard as Phase 1 (business requirement, journeys, acceptance criteria) so it can be planned directly when Phase 2 scope is confirmed.

| # | Candidate feature | Status | Origin |
|---|---|---|---|
| 2.1 | AI-Assisted Property Onboarding | Candidate | Gap found during Phase 1 planning, 2026-10-23 |

---

# 2. Feature 2.1 — AI-Assisted Property Onboarding

## 2.1.1 Objective

Let a hotel be onboarded by giving its **public website** and/or **content documents** (brochures, PDFs, fact sheets, menus, policy documents). The platform then:

1. Gathers and parses the source content.
2. Uses AI to extract **draft** canonical entities: property details, spaces, amenities, operating hours, restaurants, menus and menu items, policies, services, and events.
3. Lets an administrator review, correct, approve, or reject each draft.
4. Turns approved drafts into canonical, authoritative entities that emit the normal Phase 1 domain events.

This cuts onboarding from hours of manual data entry to a guided review.

## 2.1.2 Core Rule — Extracted Data Is Not Authoritative Until Approved

Under P4 (grounded, honest AI), AI-extracted data is a **draft**:

- Drafts are never used to answer guest questions.
- Each draft keeps its **provenance**: source URL or document, page/section, the extracted snippet, extraction model and prompt version, and a confidence score.
- Only an administrator's approval (a human decision per P2: an explicit policy gate) promotes a draft to a canonical entity.
- Raw source content may be stored straight away as `ContentItem`, flagged **descriptive / non-authoritative**, so it's searchable but never treated as policy or structured fact.

## 2.1.3 Personas

- **Property Administrator:** submits sources, reviews drafts, approves or corrects.
- **Platform Onboarding Specialist** (optional): onboards hotels on their behalf.

## 2.1.4 Scope

### In Scope
- Submit one or more website URLs (with crawl limits: same domain, max depth, max pages).
- Upload documents: PDF, DOCX, HTML, plain text, and images of menus (OCR).
- Fetch and parse into normalized text chunks, keeping the source location.
- AI extraction into draft entities for every Phase 1 configuration type.
- Deduplication and conflict detection across sources (e.g. two different pool hours).
- A review UI: grouped by entity type, side-by-side source snippet, edit-before-approve, bulk approve for high-confidence items.
- Re-running onboarding on the same sources (incremental: only new or changed content creates new drafts).
- A full trace and audit of every extraction and approval.

### Out of Scope
- Logged-in or paywalled website areas.
- Continuous automatic sync from the website (possible future feature: scheduled re-crawl with change drafts).
- Auto-approval without a human (may be revisited per property policy later).
- Images other than menus (e.g. room photo tagging).

## 2.1.5 Domain Model Additions

```text
OnboardingSession
  id, propertyId (nullable until the property is approved), status, createdBy, createdAt, completedAt

OnboardingSource
  id, sessionId, sourceType (WEBSITE | DOCUMENT), uri/blobRef, contentHash, fetchedAt, status

SourceChunk
  id, sourceId, location (url + selector | page + section), text, contentHash

DraftEntity
  id, sessionId, entityType (Property | Space | Amenity | OperatingHours | Restaurant | Menu |
                             MenuItem | Policy | Service | PropertyEvent | ContentItem)
  proposedData (canonical shape), provenance[] (chunkId, snippet), confidence,
  extractionModel, promptVersion, status, conflictsWith[], reviewedBy, reviewedAt, resultingEntityId
```

`DraftEntity` statuses: `Proposed → InReview → Approved | Edited+Approved | Rejected | Superseded`

`OnboardingSession` statuses: `Created → Fetching → Extracting → ReadyForReview → Completed | Failed | Cancelled`

## 2.1.6 Events

```text
OnboardingSessionCreated
OnboardingSourceAdded
OnboardingSourceFetched
OnboardingSourceFetchFailed
OnboardingExtractionCompleted
DraftEntityProposed
DraftEntityConflictDetected
DraftEntityApproved
DraftEntityRejected
OnboardingSessionCompleted
```

Approving a draft runs the normal Phase 1 capability (e.g. create amenity), which emits the normal Phase 1 event (e.g. `AmenityCreated`). Its causation chain links back to `DraftEntityApproved`.

## 2.1.7 Capabilities

```text
Onboarding.CreateSession
Onboarding.AddSource
Onboarding.GetSessionStatus
Onboarding.ListDrafts
Onboarding.ApproveDraft      // executes the matching Phase 1 create/update capability
Onboarding.RejectDraft
Onboarding.EditDraft
```

## 2.1.8 Ports (per P5 / P6)

| Port | Purpose | Local adapter | Azure adapter |
|---|---|---|---|
| `IWebContentFetcher` | Polite crawling (robots.txt, rate limits, domain/depth limits) | HttpClient-based | Same / Azure Functions worker |
| `IDocumentParser` | PDF/DOCX/HTML → text chunks with locations | Open-source parsers | Azure AI Document Intelligence |
| `IOcrEngine` | Menu images → text | Tesseract | Azure AI Vision |
| `IBlobStore` (Phase 1) | Store raw sources | Filesystem | Blob Storage |

Inference tasks (bound per task, P6):

```text
onboarding.classify.chunk      // which entity types a chunk contains
onboarding.extract.property
onboarding.extract.hours
onboarding.extract.policies
onboarding.extract.dining      // restaurants, menus, items, dietary attributes, prices
onboarding.extract.amenities
onboarding.extract.events
onboarding.extract.services
onboarding.reconcile           // dedupe + conflict detection across chunks
```

All extraction output is validated against the canonical schemas before a `DraftEntity` is created.

## 2.1.9 User Journeys

### Journey 2.1-A — Onboard from Website
Admin enters `https://aurora-grand.example.com`.

**Acceptance Criteria**
- Crawl respects robots.txt, stays on the domain, and honours the configured depth and page limits.
- Each fetched page is stored with a content hash; `OnboardingSourceFetched` is emitted.
- Draft entities are proposed for every recognized entity type, each with provenance and confidence.
- No canonical entity is created before approval.
- Session reaches `ReadyForReview`, and the admin is notified.

### Journey 2.1-B — Onboard from Documents
Admin uploads a spa brochure PDF and a restaurant menu photo.

**Acceptance Criteria**
- PDF text is extracted with page references; the menu image is OCR'd.
- Menu items have name, price, and dietary attributes when present; missing prices stay empty, not guessed.
- Each draft links back to its page or image region.

### Journey 2.1-C — Review and Approve
**Acceptance Criteria**
- Admin sees drafts grouped by type, with the source snippet beside the proposed data.
- Admin can edit any field before approving.
- Approval runs the matching Phase 1 capability and emits the normal domain event, with causation linked to `DraftEntityApproved`.
- Bulk approve is available only for drafts above a configurable confidence threshold and with no conflicts.
- Every approval and rejection is audited (who, when, what changed from the draft).

### Journey 2.1-D — Conflicting Sources
Website says pool closes at 9 PM; brochure says 10 PM.

**Acceptance Criteria**
- `DraftEntityConflictDetected` is emitted; both drafts are shown side by side with their sources.
- Neither can be bulk-approved; the admin must pick one or enter the correct value.

### Journey 2.1-E — Guest Asks Before Approval
Guest asks "What time does the pool close?" while pool hours are still a draft.

**Acceptance Criteria**
- The concierge does **not** use the draft and answers "unconfirmed" (Phase 1 Journey 17 behavior).
- It may cite descriptive `ContentItem` text only if clearly framed as non-authoritative, per policy.

### Journey 2.1-F — Re-run Onboarding
Admin re-submits the website a month later.

**Acceptance Criteria**
- Unchanged pages (same content hash) produce no new drafts.
- Changed content produces drafts marked as **updates** to existing canonical entities, showing a diff.
- Running the same source twice never creates duplicate drafts or duplicate entities (P8).

### Journey 2.1-G — Source Fetch Fails
**Acceptance Criteria**
- `OnboardingSourceFetchFailed` is emitted with the reason; other sources continue.
- Session shows partial results and the failure; admin can retry the failed source.

## 2.1.10 AI Evaluation Scenarios

```text
1. Extract operating hours with multiple intervals and a closed day
2. Extract a child-age gym policy without altering its meaning
3. Menu PDF → items with correct prices and vegetarian/vegan flags
4. Page with no policy information → no invented policies
5. Conflicting hours across two sources → conflict detected
6. Marketing language ("open late!") → no fabricated exact hours
7. Event listing with past and future dates → only future events proposed
8. Re-run on unchanged source → zero new drafts
```

Each scenario defines input sources, expected drafts, disallowed drafts, and expected events.

## 2.1.11 Dependencies on Phase 1

- Canonical domain entities and their create/update capabilities (Phase 1 Milestones B, D, F).
- Inference ports, router, prompt registry, structured-output validation, AI traces (Milestone G).
- Event backbone and audit (Milestone C).
- Admin web (Milestone J4); the review UI extends it.

## 2.1.12 Definition of Done

- The Aurora Grand reference property can be onboarded from a sample website plus documents, with an admin reviewing and approving the drafts.
- No guest answer uses unapproved data.
- All of the evaluation scenarios in 2.1.10 pass.
- Every approved entity is traceable to its source snippet, extraction model, prompt version, and approver.
