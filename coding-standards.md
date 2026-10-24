# Hospitality AI OS — Coding Standards (C#)

**Status:** Living document — applies to **every phase**.
**Related:** [architecture-principles.md](architecture-principles.md) · [solution-structure.md](solution-structure.md)

Standards are enforced by `.editorconfig` naming/style rules with **error** severity, `EnforceCodeStyleInBuild=true`, and `dotnet format --verify-no-changes` in CI. Code that violates them does not build.

---

## 1. Naming

### Core rules
- **Types are PascalCase** — camel case with an upper-case first character.
- **Fields are camelCase** — lower-case first character, **no `_` or `m_` prefix**.

| Element | Convention | Example |
|---|---|---|
| Class, record, struct | PascalCase | `HumanAction`, `OperatingHours` |
| Interface | `I` + PascalCase | `IEventTransport`, `IChatModel` |
| Enum type and members | PascalCase | `HumanActionStatus.InProgress` |
| Generic type parameter | `T` + PascalCase | `TPort`, `TEvent` |
| Field (private, protected, internal; instance or static, incl. `readonly`) | camelCase | `eventTransport`, `retryCount` |
| Constant (`const`) | PascalCase | `MaxRetryAttempts` |
| Property | PascalCase | `PropertyId`, `CompletedAt` |
| Method | PascalCase | `Complete()`, `GetHoursFor()` |
| Async method | PascalCase + `Async` | `PublishAsync()` |
| Event (C# `event`) | PascalCase | `StatusChanged` |
| Parameter | camelCase | `propertyId` |
| Local variable, local function | camelCase / PascalCase | `requestedAt` / `BuildContext()` |
| Namespace | PascalCase, matches folder | `HospitalityOS.Domain.Operations` |
| File | Same as the type it contains | `HumanAction.cs` |

### Field rules
- Fields are never `public`. Expose state through properties.
- When a field and a parameter share a name, qualify the field with `this.`:

```csharp
public sealed class EventDispatcher
{
    private readonly IEventTransport eventTransport;
    private readonly TimeProvider timeProvider;
    private int dispatchedCount;

    public EventDispatcher(IEventTransport eventTransport, TimeProvider timeProvider)
    {
        this.eventTransport = eventTransport;
        this.timeProvider = timeProvider;
    }
}
```

- Primary constructors are allowed. Their parameters follow parameter naming (camelCase), which matches the field convention.

### Domain-specific names
| Element | Convention | Example |
|---|---|---|
| Domain event type | PascalCase, past tense, `<Aggregate><Verb>` | `HumanActionCompleted` |
| Strongly typed ID | `<Aggregate>Id` | `ServiceRequestId` |
| Capability name (string) | `<Area>.<Action>`, PascalCase segments | `GuestService.CreateRequest` |
| Inference task name (string) | lower-case dotted | `concierge.answer` |
| Port | `I` + capability noun | `IBlobStore` |
| Adapter class | Technology + port noun | `AzureServiceBusEventTransport` |
| Options class | `<Thing>Options` | `AnthropicOptions` |

### Wire and storage formats
- JSON (API, event payloads, capability schemas): **camelCase** property names (System.Text.Json web defaults).
- Enum values on the wire: strings, not integers.
- Database tables and columns follow EF Core defaults (PascalCase entity and property names).

## 2. Language and Style

- File-scoped namespaces; `using` directives outside the namespace; implicit usings enabled.
- Nullable reference types enabled; no `!` null-forgiving operator without a comment explaining why.
- Classes are `sealed` unless designed for inheritance.
- Prefer `record` / `record struct` for value objects, IDs, DTOs, and event payloads.
- Use `var` only when the type is obvious from the right-hand side.
- Braces are always used, even for single-line `if` / `for`.
- Expression-bodied members only for single-expression members.
- Four-space indentation, LF line endings, UTF-8, final newline (set in `.editorconfig`).

## 3. Design Rules

- **Async all the way:** no `.Result` / `.Wait()`; `CancellationToken` is the last parameter of every async public method.
- **Time:** inject `TimeProvider`; never call `DateTime.Now`, `DateTime.UtcNow`, or `DateTimeOffset.Now` directly. Store timestamps as `DateTimeOffset` (UTC). Convert to property-local time only at the edge.
- **IDs:** generate through `IIdGenerator`; never call `Guid.NewGuid()` in domain or application code.
- **Errors:** expected failures (validation, illegal state transitions, not found) return `Result`/`Error`. Exceptions are for programming errors and infrastructure faults only.
- **No static mutable state.** No service locator. Dependencies come through constructors.
- **Domain purity:** domain types have no attributes or dependencies for persistence, serialization, or DI.
- **Logging:** structured logging with message templates (`logger.LogInformation("Dispatched {EventId}", eventId)`), never string interpolation in log calls. Never log secrets or unredacted guest PII.
- **Comments:** explain *why*, not *what*. XML doc comments are required on public ports and contracts.

## 4. Tests

- Test class: `<ClassUnderTest>Tests`.
- Test method: `<Method>_<Scenario>_<ExpectedResult>`, e.g. `Complete_WhenAlreadyCompleted_IsNoOp`.
- Arrange / Act / Assert sections, separated by blank lines.
- Use `FakeTimeProvider` and deterministic ID generators; no sleeps, no real clocks.

## 5. Change Log

| Date | Change |
|---|---|
| 2026-10-23 | Initial standards. Types PascalCase; fields camelCase without prefix; `this.` for disambiguation. |
