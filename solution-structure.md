# Hospitality AI OS — Solution Structure Rules

**Status:** Living document — applies to **every phase**.
**Related:** [architecture-principles.md](architecture-principles.md) (P5, P9, P13) · [coding-standards.md](coding-standards.md)

These rules define where code lives and how projects are named and wired. Phase implementation plans list the *specific* projects they add; they must follow these rules. Architecture tests and CI enforce them where possible.

---

## 1. Repository Root

```text
/
  HospitalityOS.slnx          # XML solution format (.NET 10 default)
  global.json                 # pinned .NET SDK + test runner (Microsoft.Testing.Platform)
  Directory.Build.props       # shared build settings for all projects
  Directory.Packages.props    # central package versions (only place versions appear)
  .editorconfig               # style + naming rules (see coding-standards.md)
  src/                        # production .NET code
  tests/                      # .NET test projects
  apps/                       # frontend applications
  deploy/                     # infrastructure as code
  dev/                        # seed data, local environment scripts
  .github/                    # workflows and reusable actions
  *.md                        # principles, standards, phase specs and plans
```

No production code outside `src/` and `apps/`. No deployable infrastructure outside `deploy/`.

## 2. Source Layers (`src/`)

| Folder | Contains | May reference |
|---|---|---|
| `src/Core/` | `SharedKernel`, ports (infrastructure, inference), integration contracts | `SharedKernel` only |
| `src/Domains/` | One project per domain module | `SharedKernel` only |
| `src/Platform/` | Application services, eventing, capabilities, AI, workflows, authorization, integration runtime | `Core`, `Domains`, `Contracts` |
| `src/Adapters/` | One project per technology adapter family | `Core` (+ its own vendor SDK); persistence adapters may also reference `Domains` to implement repository interfaces |
| `src/Hosts/` | Deployable hosts (composition roots) | Anything |
| `src/Contracts/` | API DTOs, event payload schemas, capability schemas | `SharedKernel` only |

Rules:
- Dependencies point inward only (P9). A lower layer never references a higher one.
- Domain projects never reference other domain projects; cross-domain links are by ID.
- Vendor SDK packages (`Azure.*`, `AWSSDK.*`, `Anthropic*`, `OpenAI*`, `Npgsql`, `StackExchange.Redis`, …) may be referenced **only** by `src/Adapters/*` projects (P5).
- `SharedKernel`, domain, and contract projects reference no packages. Port projects may reference only vendor-neutral `Microsoft.Extensions.*.Abstractions` packages.
- Only `src/Hosts/*` reference adapter projects.

## 3. Project Naming

Pattern: `HospitalityOS.<Layer-or-Kind>[.<Module>][.<Technology>]`

| Kind | Pattern | Example |
|---|---|---|
| Kernel / ports | `HospitalityOS.<Name>` | `HospitalityOS.SharedKernel`, `HospitalityOS.Ports.Inference` |
| Domain module | `HospitalityOS.Domain.<Module>` | `HospitalityOS.Domain.Operations` |
| Platform module | `HospitalityOS.<Module>` | `HospitalityOS.Capabilities` |
| Infrastructure adapter | `HospitalityOS.Adapters.<Technology>[.<Variant>]` | `HospitalityOS.Adapters.EfCore.Sqlite` |
| Inference adapter | `HospitalityOS.Adapters.Inference.<Provider>` | `HospitalityOS.Adapters.Inference.Anthropic` |
| Hospitality integration | `HospitalityOS.Integrations.<Mock\|Native\|Vendor>` | `HospitalityOS.Integrations.Mock` |
| Host | `HospitalityOS.<HostName>` under `src/Hosts/` | `HospitalityOS.Api` |
| Test | `HospitalityOS.<Kind>Tests` | `HospitalityOS.ArchitectureTests` |

- Folder name = project name = assembly name = root namespace.
- Namespaces follow folder paths: `HospitalityOS.Domain.Operations.HumanActions`.
- Solution folders in `HospitalityOS.slnx` mirror the disk layout.
- `ProjectReference` paths use forward slashes (portable across Windows, Linux CI, and macOS).

## 4. Inside a Project

### Domain module
```text
HospitalityOS.Domain.<Module>/
  <Aggregate>/                # one folder per aggregate root
    <Aggregate>.cs            # aggregate root
    <Aggregate>Id.cs          # strongly typed ID (except IDs other domains reference, e.g. PropertyId: those live in SharedKernel/Ids)
    <Aggregate>Status.cs      # enums / value objects specific to the aggregate
    I<Aggregate>Repository.cs # persistence port for this aggregate (pure interface; implemented by a persistence adapter)
    Events/                   # domain events raised by this aggregate
  Shared/                     # value objects shared within the module only
```

### Platform / application module
```text
HospitalityOS.<Module>/
  <Feature>/                  # feature folders, not technical-layer folders
  DependencyInjection.cs      # one public Add<Module>(this IServiceCollection, ...) extension
```

### Adapter project
```text
HospitalityOS.Adapters.<Technology>/
  <PortName>/                 # one folder per port implemented
  DependencyInjection.cs      # one public Add<Technology>Adapters(...) extension; registers named adapter factories
```
- Adapter implementation classes are `internal sealed`; only the DI extension and options types are public.
- Adapters translate vendor types to platform types at the boundary; vendor types never appear in public signatures.

### General
- One top-level public type per file; file name = type name.
- No `Common`, `Utils`, `Helpers`, or `Misc` projects or folders — put code where it belongs, or in `SharedKernel` if truly cross-cutting.

## 5. Build Configuration

- All shared settings live in `Directory.Build.props`: target framework, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, analyzers.
- **Central package management:** versions only in `Directory.Packages.props`; `.csproj` files use `<PackageReference Include="..." />` without versions.
- `.csproj` files stay minimal — references only, no duplicated settings.
- SDK version pinned in `global.json`.

## 6. Tests (`tests/`)

- Test kinds: `ArchitectureTests`, `PortContractTests`, `UnitTests`, `IntegrationTests`, `EndToEndTests`, `AiEvals`.
- Inside a test project, folders mirror the production namespace under test.
- **Port contract suites** are abstract test classes in `PortContractTests`; each adapter gets a concrete subclass supplying the adapter instance.
- Test projects may reference anything; production projects never reference test projects.
- A test project is created in the slice that adds its first test — no empty test projects.
- Tests run on xUnit v3 via Microsoft.Testing.Platform (`dotnet test --solution HospitalityOS.slnx`); test projects have `OutputType=Exe`.

## 7. Frontends (`apps/`)

- One folder per app, kebab-case: `guest-web`, `staff-web`, `admin-web`.
- Shared frontend code lives in `apps/packages/<name>` (e.g. generated API client); apps don't import from each other.
- API clients are generated from the OpenAPI contract, never hand-written.

## 8. Infrastructure (`deploy/`)

```text
deploy/terraform/
  modules/<cloud>/<port-module>/   # one module per infrastructure port, per cloud
  environments/<env>/              # composes modules; tfvars choose provider per binding
deploy/migrations/                 # generated idempotent migration bundles (build output)
```
- Module and resource names: kebab-case folders, snake_case Terraform identifiers (Terraform convention).

## 9. Enforcement

| Rule | Enforced by |
|---|---|
| Layer dependencies, vendor SDK isolation, adapter visibility | `HospitalityOS.ArchitectureTests` (NetArchTest) |
| Central package versions | Build (`ManagePackageVersionsCentrally`) |
| Naming and style | `.editorconfig` + `EnforceCodeStyleInBuild` + `dotnet format --verify-no-changes` in CI |

## 10. Change Log

| Date | Change |
|---|---|
| 2026-10-23 | Initial solution structure rules. |
