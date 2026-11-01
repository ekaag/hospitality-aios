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
way, so the plan, not the image, decides where events go.

## Events

The cloud transports send and receive but may not create topics, subscriptions, or queues: those come from `events.json`.
A new event handler needs its subscription there, and `EventInfrastructureTests` fails until it is added. The Service Bus
adapter's contract tests run against the emulator (`tests/emulators/servicebus`), which CI starts.

## Bootstrapping a cloud (once)

CI cannot create the identity it signs in with, so each cloud has one bootstrap, run by someone signed in with rights to
assign roles. For Azure:

```sh
az login
python deploy/tooling/deploy.py bootstrap --cloud azure --repo ekaag/hospitality-aios --environments dev --github
```

It deploys the subscription deployment stack `hospitalityos-deploy`: `rg-hospitalityos-deploy` with the managed identity
`deploy-shared-uksouth-id`, federated credentials trusting this repository's `main` branch, its pull requests, and its
`dev` environment, Contributor on the subscription, and permission to grant only AcrPull, Key Vault Secrets User, and Azure
Service Bus Data Sender and Data Receiver.
`--github` sets the repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` and creates
the `dev` environment with `gh`. Running it again changes nothing; `bootstrap-down` removes it all.

## Workflows

| Workflow | When | Does |
|---|---|---|
| `iac-validate.yml` | PRs and pushes touching `deploy/` | Orchestrator tests, every plan validated, every module built and linted; a what-if comment on PRs |
| `deploy-dev.yml` | After CI passes on `main`, or on demand with a plan | `deploy.yml` for `dev` |
| `deploy.yml` | Reused | `deploy.py up` for one environment and plan |
| `drift-detection.yml` | Daily | `deploy.py drift` for `dev`; opens an issue on drift |
| `teardown.yml` | On demand, environment typed again | `deploy.py down` |

The deploying workflows skip themselves until the bootstrap has set the variables.
