"""The orchestrator's command line: validate and show plans, bootstrap a cloud's deployment identity, deploy, check for
drift, and tear down. Workflows and people run the same commands."""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from typing import Callable

from . import azure, graph
from .deployer import Deployer
from .model import DEFAULT_PLAN, PlanError, configuration, load_catalog, load_plan, plan_names

DRIVERS: dict[str, Callable[[], object]] = {"azure": azure.AzureDriver, "graph": graph.GraphDriver}


def choose_plan(given: str | None, ask: Callable[[str], str] = input, interactive: bool | None = None) -> str:
    """The plan to use: the one given, else asked for with the default offered, else (unattended) the default."""
    if given:
        return given

    if interactive is None:
        interactive = sys.stdin.isatty()

    if not interactive:
        print(f"No plan given; using the default plan '{DEFAULT_PLAN}'.")
        return DEFAULT_PLAN

    print(f"Deployment plans: {', '.join(plan_names())}")
    answer = ask(f"Use which plan? [{DEFAULT_PLAN}] ").strip()

    return answer or DEFAULT_PLAN


def deployer(env: str, plan_name: str, promote_from: str | None = None) -> Deployer:
    catalog = load_catalog()
    plan = load_plan(plan_name, env, catalog)

    if promote_from is not None and (promote_from == env or promote_from not in catalog.get("environments", [])):
        raise PlanError([f"Promote from another of the catalog's environments ({', '.join(catalog.get('environments', []))}), not '{promote_from}'."])

    return Deployer(plan, env, catalog["product"], {driver: DRIVERS[driver]() for driver in plan.drivers()}, promote_from=promote_from)


def validate(arguments: argparse.Namespace) -> int:
    catalog = load_catalog()
    names = [arguments.plan] if arguments.plan else plan_names()
    environments = [arguments.env] if arguments.env else catalog.get("environments", [])
    failed = False

    for name in names:
        for env in environments:
            try:
                plan = load_plan(name, env, catalog)
                print(f"ok      {name} ({env}): {len(plan.placements)} resources on {', '.join(plan.clouds())}")
            except PlanError as error:
                failed = True
                print(f"invalid {name} ({env}):")
                for problem in error.problems:
                    print(f"  - {problem}")

    return 1 if failed else 0


def show(arguments: argparse.Namespace) -> int:
    planned = deployer(arguments.env, choose_plan(arguments.plan))
    print(f"Plan {planned.plan.name} for {arguments.env}, in deployment order:")

    for target in planned.targets():
        placement = target.placement
        print(f"  {placement.purpose:<15} {placement.cloud:<6} {placement.region:<12} {target.name:<32} {target.group}")

    return 0


def bootstrap(arguments: argparse.Namespace) -> int:
    if arguments.cloud != "azure":
        print(f"There is no bootstrap for {arguments.cloud} yet.")
        return 1

    account = azure.signed_in()
    if not account:
        print("Sign in first: az login (an account that may assign roles on the subscription).")
        return 1

    print(f"Bootstrapping Azure subscription {account['name']} ({account['id']}) for {arguments.repo}.")
    values = azure.bootstrap(load_catalog()["product"], arguments.region, arguments.repo, arguments.environments)

    for key, value in values.items():
        print(f"  {key}={value}")

    if arguments.github:
        github(arguments.repo, values, arguments.environments, arguments.reviewer)

    return 0


def bootstrap_down(arguments: argparse.Namespace) -> int:
    if arguments.cloud != "azure":
        print(f"There is no bootstrap for {arguments.cloud} yet.")
        return 1

    azure.bootstrap_down()

    if arguments.github:
        for key in ("AZURE_CLIENT_ID", "AZURE_TENANT_ID", "AZURE_SUBSCRIPTION_ID"):
            subprocess.run(["gh", "variable", "delete", key, "--repo", arguments.repo], check=False)

    print("Removed the Azure deployment identity.")
    return 0


def bootstrap_signin(arguments: argparse.Namespace) -> int:
    if not azure.signed_in():
        print("Sign in first: az login (an account that may create resources in the subscription).")
        return 1

    product = load_catalog()["product"]
    values = graph.bootstrap_signin(
        product, arguments.env, arguments.region, arguments.domain, arguments.country, arguments.data_location, arguments.repo, arguments.device_code,
    )

    for key, value in values.items():
        print(f"  {key}={value}")

    if arguments.github:
        names = graph.github_variables(arguments.env)
        for key, value in values.items():
            subprocess.run(["gh", "variable", "set", names[key], "--body", value, "--repo", arguments.repo], check=True)

    return 0


def bootstrap_signin_down(arguments: argparse.Namespace) -> int:
    if arguments.confirm != arguments.env:
        print(f"To remove {arguments.env}'s External ID tenant, pass --confirm {arguments.env}.")
        return 1

    graph.bootstrap_signin_down(load_catalog()["product"], arguments.env, arguments.region, arguments.tenant)

    if arguments.github:
        for name in graph.github_variables(arguments.env).values():
            subprocess.run(["gh", "variable", "delete", name, "--repo", arguments.repo], check=False)

    print(f"Removed {arguments.env}'s External ID tenant.")
    return 0


def operator(arguments: argparse.Namespace) -> int:
    """Grants or revokes the Operator role, held by those who may invite a customer's first administrator."""
    planned = deployer(arguments.env, choose_plan(arguments.plan))
    target = next((target for target in planned.targets() if target.placement.driver == "graph"), None)

    if target is None:
        print("The plan places no sign-in resource.")
        return 1

    driver = planned.driver(target)
    api = graph.desired(graph.load_definition(target.placement.module), target.name, arguments.env)["api"]["uniqueName"]
    granting = arguments.command == "grant-operator"
    who = (graph.grant_operator if granting else graph.revoke_operator)(driver.graph, api, arguments.email)
    print(f"{'Granted' if granting else 'Revoked'} Operator for {who} in {arguments.env}; it applies from their next sign-in.")

    return 0


# Environments only main may deploy to, and those a reviewer must approve each deployment to.
MAIN_ONLY = {"test", "prod"}
REVIEWED = {"prod"}


def environment_settings(environment: str, reviewer_id: int | None) -> dict:
    """A GitHub deployment environment's protection: test and prod only from main; prod waits for its reviewer."""
    body: dict = {"deployment_branch_policy": None}

    if environment in MAIN_ONLY:
        body["deployment_branch_policy"] = {"protected_branches": False, "custom_branch_policies": True}

    if environment in REVIEWED:
        if reviewer_id is None:
            raise PlanError([f"{environment} needs a reviewer to approve its deployments."])
        body["reviewers"] = [{"type": "User", "id": reviewer_id}]
        body["prevent_self_review"] = False

    return body


def github(repository: str, values: dict[str, str], environments: list[str], reviewer: str | None = None) -> None:
    """Sets the repository variables the workflows sign in with, and creates the deployment environments with their
    protection rules (the reviewer, by default the repository's owner, approves prod)."""
    if not shutil.which("gh"):
        raise PlanError(["The GitHub CLI (gh) is needed to set the repository variables."])

    for key, value in values.items():
        subprocess.run(["gh", "variable", "set", key, "--body", value, "--repo", repository], check=True)

    login = reviewer or repository.split("/", 1)[0]
    reviewer_id = None
    if REVIEWED & set(environments):
        reviewer_id = int(subprocess.run(["gh", "api", f"users/{login}", "--jq", ".id"], check=True, capture_output=True, text=True).stdout.strip())

    for environment in environments:
        subprocess.run(
            ["gh", "api", "--method", "PUT", f"repos/{repository}/environments/{environment}", "--input", "-", "--silent"],
            input=json.dumps(environment_settings(environment, reviewer_id)), text=True, check=True,
        )

        if environment in MAIN_ONLY:
            policies = subprocess.run(
                ["gh", "api", f"repos/{repository}/environments/{environment}/deployment-branch-policies", "--jq", "[.branch_policies[].name]"],
                check=True, capture_output=True, text=True,
            ).stdout
            if "main" not in json.loads(policies or "[]"):
                subprocess.run(
                    ["gh", "api", "--method", "POST", f"repos/{repository}/environments/{environment}/deployment-branch-policies", "-f", "name=main", "-f", "type=branch", "--silent"],
                    check=True,
                )


def up(arguments: argparse.Namespace) -> int:
    outputs = deployer(arguments.env, choose_plan(arguments.plan), arguments.promote_from).up(arguments.sha, smoke=not arguments.no_smoke)
    api = outputs.get("api", {})

    if api.get("url"):
        print(f"API: {api['url']}")

    return 0


def drift(arguments: argparse.Namespace) -> int:
    planned = deployer(arguments.env, choose_plan(arguments.plan))
    drifted = planned.drift()

    # An environment never deployed (e.g. prod before its first promotion) has nothing to drift from.
    if arguments.skip_undeployed and planned.nothing_deployed(drifted):
        print(f"{arguments.env} ({planned.plan.name}) is not deployed yet; nothing to compare.")
        return 0

    lines = [f"Drift in {arguments.env} ({planned.plan.name}):"] if drifted else [f"No drift in {arguments.env} ({planned.plan.name})."]

    for purpose, changes in drifted.items():
        lines.append(f"- {purpose}")
        lines.extend(f"  - {change}" for change in changes)

    print("\n".join(lines))

    if arguments.report:
        with open(arguments.report, "w", encoding="utf-8") as file:
            file.write("\n".join(lines) + "\n")

    return 1 if drifted else 0


def settings(arguments: argparse.Namespace) -> int:
    """Prints the deployed environment's app settings starting with the prefix: NAME=value lines, or with --json an
    appsettings.json document (the live AI evals bind the environment's models with it)."""
    deployed = deployer(arguments.env, choose_plan(arguments.plan)).deployed_settings()
    chosen = {key: value for key, value in sorted(deployed.items()) if key.startswith(arguments.prefix)}

    if not chosen:
        print(f"{arguments.env} has no deployed app settings starting with {arguments.prefix}.", file=sys.stderr)
        return 1

    print(json.dumps(configuration(chosen), indent=2) if arguments.json else "\n".join(f"{key}={value}" for key, value in chosen.items()))

    return 0


def down(arguments: argparse.Namespace) -> int:
    # Naming the environment again guards against tearing down the wrong one.
    if arguments.confirm != arguments.env:
        print(f"To tear down {arguments.env}, pass --confirm {arguments.env}.")
        return 1

    kept = deployer(arguments.env, choose_plan(arguments.plan)).down(include_stateful=arguments.include_stateful)
    print(f"Tore down {arguments.env}" + (f"; kept {', '.join(kept)}." if kept else "."))

    return 0


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(prog="deploy", description="Deploys HospitalityOS from a deployment plan (deploy/plans).")
    commands = root.add_subparsers(dest="command", required=True)

    def planned(name: str, help_text: str, handler) -> argparse.ArgumentParser:
        command = commands.add_parser(name, help=help_text)
        command.add_argument("--env", required=True, help="The environment, e.g. dev.")
        command.add_argument("--plan", help=f"The deployment plan; asked for when omitted (default {DEFAULT_PLAN}).")
        command.set_defaults(handler=handler)
        return command

    check = commands.add_parser("validate", help="Checks plans against the catalog and modules (all plans by default).")
    check.add_argument("--plan")
    check.add_argument("--env")
    check.set_defaults(handler=validate)

    planned("show", "Lists a plan's resources in deployment order with their names.", show)

    deploy_up = planned("up", "Deploys a plan (idempotent: the same SHA again changes nothing).", up)
    deploy_up.add_argument("--sha", required=True, help="The commit the images are built from and tagged with.")
    deploy_up.add_argument("--no-smoke", action="store_true", help="Skip the health check after deploying.")
    deploy_up.add_argument("--promote-from", help="Copy the images from this environment's registry instead of building them.")

    deploy_settings = planned("settings", "Prints a deployed environment's app settings with a prefix, e.g. Inference__.", settings)
    deploy_settings.add_argument("--prefix", required=True)
    deploy_settings.add_argument("--json", action="store_true", help="Print an appsettings.json document instead of NAME=value lines.")

    deploy_drift = planned("drift", "Reports what deploying again would change; exits 1 on drift.", drift)
    deploy_drift.add_argument("--report", help="Also write the report to this file.")
    deploy_drift.add_argument("--skip-undeployed", action="store_true", help="Succeed when nothing of the plan is deployed yet.")

    deploy_down = planned("down", "Tears down a plan's resources in reverse order.", down)
    deploy_down.add_argument("--confirm", help="The environment's name again.")
    deploy_down.add_argument("--include-stateful", action="store_true", help="Also delete databases, storage, and vaults.")

    for name, handler, help_text in (
        ("bootstrap", bootstrap, "Creates a cloud's deployment identity and its GitHub OIDC trust (run once, signed in)."),
        ("bootstrap-down", bootstrap_down, "Removes a cloud's deployment identity."),
    ):
        command = commands.add_parser(name, help=help_text)
        command.add_argument("--cloud", default="azure")
        command.add_argument("--repo", required=True, help="The GitHub repository, owner/name.")
        command.add_argument("--region", default="uksouth")
        command.add_argument("--environments", nargs="+", default=load_catalog().get("environments", ["dev"]))
        command.add_argument("--github", action="store_true", help="Also set (or remove) the repository variables with gh, and create the deployment environments.")
        command.add_argument("--reviewer", help="Who approves prod deployments (a GitHub login); the repository's owner by default.")
        command.set_defaults(handler=handler)

    signin = commands.add_parser("bootstrap-signin", help="Creates an environment's Entra External ID tenant and its deployment app (run once, as an administrator).")
    signin.add_argument("--env", required=True)
    signin.add_argument("--repo", required=True, help="The GitHub repository, owner/name.")
    signin.add_argument("--domain", required=True, help="The tenant's domain prefix, unique across Entra, e.g. hospitalityosdev.")
    signin.add_argument("--region", default="uksouth")
    signin.add_argument("--country", default="GB", help="Where the tenant's data is kept.")
    signin.add_argument("--data-location", default="Europe")
    signin.add_argument("--github", action="store_true", help="Also set the environment's variables with gh.")
    signin.add_argument("--device-code", action="store_true", help="Sign in to the new tenant with a device code, where there is no browser (the bootstrap workflow).")
    signin.set_defaults(handler=bootstrap_signin)

    signin_down = commands.add_parser("bootstrap-signin-down", help="Removes an environment's External ID tenant (after tearing down its signin resource).")
    signin_down.add_argument("--env", required=True)
    signin_down.add_argument("--repo", required=True)
    signin_down.add_argument("--tenant", required=True, help="The tenant's ID.")
    signin_down.add_argument("--region", default="uksouth")
    signin_down.add_argument("--confirm", help="The environment's name again.")
    signin_down.add_argument("--github", action="store_true")
    signin_down.set_defaults(handler=bootstrap_signin_down)

    for name, help_text in (
        ("grant-operator", "Makes someone who has signed up an operator, who may invite customers' first administrators."),
        ("revoke-operator", "Takes the Operator role away."),
    ):
        command = planned(name, help_text, operator)
        command.add_argument("--email", required=True, help="The email they signed up with.")

    return root


def main(argv: list[str] | None = None) -> int:
    arguments = parser().parse_args(argv)

    try:
        return arguments.handler(arguments)
    except PlanError as error:
        for problem in error.problems:
            print(f"error: {problem}", file=sys.stderr)
        return 1

