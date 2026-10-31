"""The orchestrator's command line: validate and show plans, bootstrap a cloud's deployment identity, deploy, check for
drift, and tear down. Workflows and people run the same commands."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from typing import Callable

from . import azure
from .deployer import Deployer
from .model import DEFAULT_PLAN, PlanError, load_catalog, load_plan, plan_names

DRIVERS: dict[str, Callable[[], object]] = {"azure": azure.AzureDriver}


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


def deployer(env: str, plan_name: str) -> Deployer:
    catalog = load_catalog()
    plan = load_plan(plan_name, env, catalog)

    return Deployer(plan, env, catalog["product"], {cloud: DRIVERS[cloud]() for cloud in plan.clouds()})


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
        print(f"  {placement.purpose:<14} {placement.cloud:<6} {placement.region:<12} {target.name:<32} {target.group}")

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
        github(arguments.repo, values, arguments.environments)

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


def github(repository: str, values: dict[str, str], environments: list[str]) -> None:
    """Sets the repository variables the workflows sign in with, and creates the deployment environments."""
    if not shutil.which("gh"):
        raise PlanError(["The GitHub CLI (gh) is needed to set the repository variables."])

    for key, value in values.items():
        subprocess.run(["gh", "variable", "set", key, "--body", value, "--repo", repository], check=True)

    for environment in environments:
        subprocess.run(["gh", "api", "--method", "PUT", f"repos/{repository}/environments/{environment}", "--silent"], check=True)


def up(arguments: argparse.Namespace) -> int:
    outputs = deployer(arguments.env, choose_plan(arguments.plan)).up(arguments.sha, smoke=not arguments.no_smoke)
    api = outputs.get("api", {})

    if api.get("url"):
        print(f"API: {api['url']}")

    return 0


def drift(arguments: argparse.Namespace) -> int:
    planned = deployer(arguments.env, choose_plan(arguments.plan))
    drifted = planned.drift()
    lines = [f"Drift in {arguments.env} ({planned.plan.name}):"] if drifted else [f"No drift in {arguments.env} ({planned.plan.name})."]

    for purpose, changes in drifted.items():
        lines.append(f"- {purpose}")
        lines.extend(f"  - {change}" for change in changes)

    print("\n".join(lines))

    if arguments.report:
        with open(arguments.report, "w", encoding="utf-8") as file:
            file.write("\n".join(lines) + "\n")

    return 1 if drifted else 0


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

    deploy_drift = planned("drift", "Reports what deploying again would change; exits 1 on drift.", drift)
    deploy_drift.add_argument("--report", help="Also write the report to this file.")

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
        command.add_argument("--environments", nargs="+", default=["dev"])
        command.add_argument("--github", action="store_true", help="Also set (or remove) the repository variables with gh.")
        command.set_defaults(handler=handler)

    return root


def main(argv: list[str] | None = None) -> int:
    arguments = parser().parse_args(argv)

    try:
        return arguments.handler(arguments)
    except PlanError as error:
        for problem in error.problems:
            print(f"error: {problem}", file=sys.stderr)
        return 1

