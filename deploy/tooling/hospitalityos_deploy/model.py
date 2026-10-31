"""The catalog, deployment plans, names, and the order resources are deployed in. Nothing here talks to a cloud."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path

DEPLOY = Path(__file__).resolve().parents[2]
"""The deploy/ folder: catalog.json, plans/, and one folder per cloud."""

DEFAULT_PLAN = "azure-only"

_SEGMENT = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")


class PlanError(Exception):
    """A plan that cannot be deployed, with every reason."""

    def __init__(self, problems: list[str]):
        super().__init__("\n".join(problems))
        self.problems = problems


@dataclass(frozen=True)
class Placement:
    """One resource of a plan: what it is, where it goes, and how it is deployed there."""

    purpose: str
    cloud: str
    region: str
    group: str
    depends_on: tuple[str, ...]
    service: dict
    stateful: bool = False
    image: dict | None = None

    @property
    def module(self) -> Path:
        return DEPLOY / self.service["module"]


@dataclass
class Plan:
    name: str
    description: str
    placements: list[Placement] = field(default_factory=list)

    def by_purpose(self) -> dict[str, Placement]:
        return {placement.purpose: placement for placement in self.placements}

    def clouds(self) -> list[str]:
        return sorted({placement.cloud for placement in self.placements})


def load_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as file:
        return json.load(file)


def load_catalog(deploy: Path = DEPLOY) -> dict:
    return load_json(deploy / "catalog.json")


def plan_names(deploy: Path = DEPLOY) -> list[str]:
    return sorted(path.stem for path in (deploy / "plans").glob("*.json"))


def resource_name(purpose: str, env: str, region: str, kind: str, compact: bool = False) -> str:
    """`<purpose>-<env>-<region>-<type>`, lowercase; run together for types that allow no delimiters."""
    segments = [purpose, env, region, kind]
    for segment in segments:
        if not _SEGMENT.match(segment):
            raise ValueError(f"'{segment}' is not a lowercase name segment.")
    name = "-".join(segments)

    return name.replace("-", "") if compact else name


def group_name(product: str, group: str, env: str) -> str:
    """`rg-<product>-<group>-<env>`: an Azure resource group per lifecycle group and environment."""
    return f"rg-{product}-{group}-{env}"


def tags(product: str, purpose: str, env: str, delimited: str) -> dict[str, str]:
    """What every resource is tagged with; `name` keeps the delimited form where the real name cannot."""
    return {"name": delimited, "purpose": purpose, "env": env, "product": product}


def load_plan(name: str, env: str, catalog: dict | None = None, deploy: Path = DEPLOY) -> Plan:
    """Reads and validates a plan for an environment; raises PlanError with every problem found."""
    catalog = catalog if catalog is not None else load_catalog(deploy)
    path = deploy / "plans" / f"{name}.json"

    if not path.exists():
        raise PlanError([f"There is no plan '{name}'. Plans: {', '.join(plan_names(deploy)) or '(none)'}."])

    document = load_json(path)
    plan = Plan(document.get("name", name), document.get("description", ""))
    problems: list[str] = []
    resources = catalog["resources"]
    chosen = document.get("resources", {})

    if env not in catalog.get("environments", []):
        problems.append(f"'{env}' is not an environment the catalog names ({', '.join(catalog.get('environments', []))}).")

    for purpose, where in chosen.items():
        entry = resources.get(purpose)

        if entry is None:
            problems.append(f"{purpose}: not in the catalog.")
            continue

        cloud, region = where.get("cloud"), where.get("region")
        service = entry["services"].get(cloud) if cloud else None

        if cloud not in catalog["clouds"]:
            problems.append(f"{purpose}: '{cloud}' is not a cloud the catalog knows.")
            continue

        if catalog["clouds"][cloud]["status"] != "available":
            problems.append(f"{purpose}: {cloud} is {catalog['clouds'][cloud]['status']}; it has no deployment tooling yet.")
            continue

        if service is None:
            problems.append(f"{purpose}: {cloud} offers no service for it.")
            continue

        if "module" not in service:
            problems.append(f"{purpose}: {cloud} ({service['service']}) has no module yet" + (f" (planned in {entry['planned']})." if "planned" in entry else "."))
            continue

        if not (deploy / service["module"]).exists():
            problems.append(f"{purpose}: its module {service['module']} is missing.")
            continue

        if not region or not _SEGMENT.match(region):
            problems.append(f"{purpose}: needs a lowercase region, e.g. uksouth.")
            continue

        name = resource_name(purpose, env, region, service["type"], service.get("compact", False))
        limit = service.get("maxLength")

        if limit and len(name) > limit:
            problems.append(f"{purpose}: its name {name} is {len(name)} characters; {cloud} allows {limit}.")

        plan.placements.append(
            Placement(
                purpose=purpose,
                cloud=cloud,
                region=region,
                group=entry["group"],
                depends_on=tuple(entry.get("dependsOn", [])),
                service=service,
                stateful=entry.get("stateful", False),
                image=entry.get("image"),
            )
        )

    # Every resource the app needs now must be placed; those still planned may wait.
    for purpose, entry in resources.items():
        if "planned" not in entry and purpose not in chosen:
            problems.append(f"{purpose}: the plan does not place it, and the app needs it.")

    placed = set(chosen)
    for placement in plan.placements:
        for dependency in placement.depends_on:
            if dependency not in placed:
                problems.append(f"{placement.purpose}: needs {dependency}, which the plan does not place.")

        for parameter, source in placement.service.get("inputs", {}).items():
            dependency = source.split(".", 1)[0]
            if dependency not in placement.depends_on:
                problems.append(f"{placement.purpose}: input {parameter} comes from {dependency}, which it does not depend on.")

    if problems:
        raise PlanError(problems)

    plan.placements = ordered(plan.placements)

    return plan


def ordered(placements: list[Placement]) -> list[Placement]:
    """Dependencies first; ties keep the plan's order. Raises PlanError on a cycle."""
    remaining = {placement.purpose: placement for placement in placements}
    done: list[Placement] = []
    seen: set[str] = set()

    while remaining:
        ready = [placement for placement in remaining.values() if all(dependency in seen for dependency in placement.depends_on)]

        if not ready:
            raise PlanError([f"Resources depend on each other in a cycle: {', '.join(sorted(remaining))}."])

        for placement in ready:
            done.append(placement)
            seen.add(placement.purpose)
            del remaining[placement.purpose]

    return done


def resolve_inputs(placement: Placement, outputs: dict[str, dict]) -> dict:
    """The module's inputs from the outputs of the resources it depends on, e.g. images.loginServer."""
    resolved = {}

    for parameter, source in placement.service.get("inputs", {}).items():
        purpose, output = source.split(".", 1)
        value = outputs.get(purpose, {}).get(output)

        if value is None:
            raise PlanError([f"{placement.purpose}: input {parameter} needs {source}, which {purpose} has not output."])

        resolved[parameter] = value

    return resolved


def app_settings(plan: Plan, outputs: dict[str, dict]) -> dict[str, str]:
    """The app's binding settings the plan implies: each placed service's `appSettings`, with `${purpose.output}` filled in."""
    settings: dict[str, str] = {}

    for placement in plan.placements:
        for key, template in placement.service.get("appSettings", {}).items():
            settings[key] = re.sub(
                r"\$\{([a-z0-9-]+)\.([A-Za-z0-9]+)\}",
                lambda match: str(outputs.get(match.group(1), {}).get(match.group(2), "")),
                template,
            )

    return settings
