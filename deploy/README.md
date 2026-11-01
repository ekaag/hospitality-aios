# Deployment

Everything in the cloud is built and torn down by automation from this folder (P12); nothing is created by hand.

| Path | What |
|---|---|
| `catalog.json` | Every resource the product needs, by its job (the purpose), its dependencies, and the service each cloud offers for it |
| `events.json` | The event topic, one subscription per event handler, and the scheduled-work queue, for whichever cloud hosts `events` |
| `plans/` | Saved deployment plans: a cloud and region for every resource. `azure-only` is the default |
| `azure/` | Azure IaC in Bicep: `bootstrap/` (the GitHub deployment identity) and `modules/`, one per resource |
| `tooling/` | The orchestrator: `python deploy/tooling/deploy.py <command>` (Python 3, standard library only) |

The cloud is chosen per resource, not per system: a plan could put `blobs` on Azure and `data` on another cloud. Each
cloud's modules use that cloud's native IaC (Azure: Bicep; AWS: CloudFormation, when a plan first uses AWS; GCP: to be
agreed). A plan is refused unless every resource it places has a module for its cloud.

## Commands

```sh
python deploy/tooling/deploy.py validate                         # every plan against the catalog and modules
python deploy/tooling/deploy.py show --env dev                   # a plan's resources, names, and order
python deploy/tooling/deploy.py up --env dev --sha <commit>      # deploy (asks which plan; Enter takes azure-only)
python deploy/tooling/deploy.py drift --env dev                  # what deploying again would change; exit 1 on drift
python deploy/tooling/deploy.py down --env dev --confirm dev     # tear down; stateful resources need --include-stateful
```

Every command takes `--plan`; without it, people are asked (the default `azure-only` is offered) and workflows get the
default. `up` deploys each resource in dependency order as its own deployment stack, passes each one's outputs to those
that need them, builds and pushes the API image tagged by the commit into the registry the plan chose (unless that tag is
already there), and waits for `/health`. Running it again on the same commit changes nothing.

## Names

`<purpose>-<env>-<region>-<type>`, lowercase (`api-dev-uksouth-ca`); Azure resource groups
`rg-hospitalityos-<purpose>-<env>` by lifecycle (`platform`, `api`, `web`); types that allow no delimiters run the segments
together (`imagesdevuksouthacr`). Every resource is tagged `name` (the delimited form), `purpose`, `env`, and `product`.
See [solution-structure.md §8](../solution-structure.md).

## App settings

A resource's catalog entry can name the app settings it implies (`appSettings`, with `${purpose.output}` filled from its
outputs); the API is deployed with all of them. `events` binds the event transport and the scheduler to Service Bus this
way, as `blobs` binds the blob store and `secrets` the secret store, so the plan, not the image, decides where events,
files, and secrets go.

## Events

The cloud transports send and receive but may not create topics, subscriptions, or queues: those come from `events.json`.
A new event handler needs its subscription there, and `EventInfrastructureTests` fails until it is added. The Service Bus
adapter's contract tests run against the emulator (`tests/emulators/servicebus`), which CI starts.

## Search indexes

Azure AI Search indexes are not ARM resources, so a service's catalog entry lists their definitions (`indexes`, in
`azure/search/`) and `up` applies them after deploying the service, as whoever runs the deployment, whom the module lets
manage indexes (`deployer.principalId`); `drift` reports indexes that are missing or whose fields differ. The API itself
may only read and write documents. A vector index's width is fixed when it is made: `SearchInfrastructureTests` keeps it
equal to what `content.embed` produces.

## Staff sign-in (Entra External ID)

Staff sign in through each environment's Microsoft Entra External ID tenant: a tenant of its own, not the subscription's,
so the product's users never mix with the operator's own staff, and each customer's identity provider can be federated
into it later. What the tenant holds is not an ARM resource and an external tenant has no subscription, so the `signin`
resource names the `graph` driver: `up` applies `azure/signin/apps.json` through Microsoft Graph (the API's registration
with its `access_as_user` scope and `Operator` app role, one single-page app registration per staff app with its
redirect URIs and consent, and the email one-time code sign-up and sign-in user flow), `drift` compares them, and `down`
deletes them for good. Its outputs set the API's `Authentication:Schemes:StaffSignIn` (authority, audience, issuer); the
staff apps take `spaAuthority`, their client IDs, and `scope` as `VITE_SIGNIN_*` build variables when they are deployed.

The driver signs in to the tenant as the tenant's own deployment app, trusted like the Azure identity (GitHub OIDC, no
secret). Who a sign-in acts as is kept by the API (StaffAccess, invited with one-time codes), not by the tenant.

Operators, who may invite a customer's first administrator, are granted after they have signed up once:

```sh
python deploy/tooling/deploy.py grant-operator --env dev --email someone@example.com
python deploy/tooling/deploy.py revoke-operator --env dev --email someone@example.com
```

## Guest check-in links

The API signs guests' stay tokens with the `stay-tokens` RSA key in the `secrets` vault, through Key Vault's sign
operation (the API holds Key Vault Crypto User on that key only, so it can sign and verify but never read the private
key). The key rotates every 90 days; guest apps then open new sessions from the same link. Links name the guest app's
address from `GuestLinks:BaseUrl`, which waits for the guest app's own deployment; until then the front desk gets the
code only.

## Per-environment settings

A service's `parameters` set module parameters per environment, e.g. `"sku": { "dev": "free" }`; every environment
needs a value.

## Bootstrapping a cloud (once)

CI cannot create the identity it signs in with, so each cloud has one bootstrap, run by someone signed in with rights to
assign roles. For Azure:

```sh
az login
python deploy/tooling/deploy.py bootstrap --cloud azure --repo ekaag/hospitality-aios --environments dev --github
```

It deploys the subscription deployment stack `hospitalityos-deploy`: `rg-hospitalityos-deploy` with the managed identity
`deploy-shared-uksouth-id`, federated credentials trusting this repository's `main` branch, its pull requests, and its
`dev` environment, Contributor on the subscription, and permission to grant only AcrPull, Key Vault Secrets User, Key Vault Crypto User, Azure
Service Bus Data Sender and Data Receiver, Storage Blob Data Contributor, Search Index Data Contributor, SignalR App
Server, and Search Service Contributor.
`--github` sets the repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` and creates
the `dev` environment with `gh`. Running it again changes nothing; `bootstrap-down` removes it all.

Each environment's External ID tenant is bootstrapped once too, by someone who may create resources in the subscription:

```sh
az login
python deploy/tooling/deploy.py bootstrap-signin --env dev --repo ekaag/hospitality-aios --domain hospitalityosdev --github
```

It creates `rg-hospitalityos-signin-dev` and in it the tenant `hospitalityosdev.onmicrosoft.com` (`azure/bootstrap/signin.bicep`,
a preview resource type; creating a tenant can take 30 minutes), then has you sign in to the new tenant as its
administrator (`az login --tenant ... --allow-no-subscriptions`) and creates there the deployment app `deploy-dev-uksouth-app`:
federated credentials for `main`, pull requests, and the `dev` environment, and only the Graph permissions the driver
needs (Application.ReadWrite.OwnedBy, DelegatedPermissionGrant.ReadWrite.All, AppRoleAssignment.ReadWrite.All,
EventListener.ReadWrite.All, User.Read.All). `--github` sets `SIGNIN_TENANT_ID_DEV`, `SIGNIN_DOMAIN_DEV`, and
`SIGNIN_CLIENT_ID_DEV`, repository variables (not environment-scoped) so drift checks and pull request previews read them
too. Running it again changes nothing. `bootstrap-signin-down --env dev --tenant <id> --confirm dev` removes the
deployment app and the tenant; Azure refuses to delete a tenant that still has users or apps, so tear the `signin`
resource down first and remove staff accounts in the Entra admin center.

## Workflows

| Workflow | When | Does |
|---|---|---|
| `iac-validate.yml` | PRs and pushes touching `deploy/` | Orchestrator tests, every plan validated, every module built and linted; a what-if comment on PRs |
| `deploy-dev.yml` | After CI passes on `main`, or on demand with a plan | `deploy.yml` for `dev` |
| `deploy.yml` | Reused | `deploy.py up` for one environment and plan |
| `drift-detection.yml` | Daily | `deploy.py drift` for `dev`; opens an issue on drift |
| `teardown.yml` | On demand, environment typed again | `deploy.py down` |

The deploying workflows skip themselves until the bootstrap has set the variables.
