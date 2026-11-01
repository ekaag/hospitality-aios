"""Deploys, previews, and tears down a plan resource by resource, through each resource's cloud driver."""

from __future__ import annotations

import os
import time
import urllib.error
import urllib.request
from collections.abc import Mapping
from dataclasses import dataclass, field
from typing import Callable, Protocol

from pathlib import Path

from .model import DEPLOY, DEPLOYER, Placement, Plan, PlanError, app_settings, environment_parameters, group_name, resolve_inputs, resource_name, secure_inputs, tags, uses_deployer


@dataclass(frozen=True)
class Target:
    """Where one resource goes, in its cloud's terms."""

    placement: Placement
    env: str
    product: str

    @property
    def name(self) -> str:
        service = self.placement.service
        return resource_name(self.placement.purpose, self.env, self.placement.region, service["type"], service.get("compact", False))

    @property
    def group(self) -> str:
        return group_name(self.product, self.placement.group, self.env)

    @property
    def stack(self) -> str:
        """The cloud's deployment record for this resource, e.g. the Azure deployment stack `api-dev`."""
        return f"{self.placement.purpose}-{self.env}"

    @property
    def tags(self) -> dict[str, str]:
        service = self.placement.service
        return tags(self.product, self.placement.purpose, self.env, resource_name(self.placement.purpose, self.env, self.placement.region, service["type"]))


class Driver(Protocol):
    """What the orchestrator needs from a cloud. Every call is idempotent."""

    def deploy(self, target: Target, parameters: dict) -> dict:
        """Creates or updates the resource and returns its outputs."""

    def outputs(self, target: Target) -> dict | None:
        """The deployed resource's outputs, or None when it is not deployed."""

    def changes(self, target: Target, parameters: dict) -> list[str]:
        """What deploying with these parameters would change (empty: no drift)."""

    def remove(self, target: Target) -> None:
        """Deletes the resource and its deployment record; nothing when it is not deployed."""

    def finish_removal(self, targets: list[Target]) -> None:
        """Cleans up what the removed resources leave, e.g. empty resource groups."""

    def run(self, target: Target, outputs: dict) -> None:
        """Runs a deployed job to completion, e.g. the migration step; raises PlanError if it fails."""

    def promote_image(self, target: Target, registry: dict, source_registry: str, repository: str, tag: str) -> None:
        """Copies an image already tested in another environment's registry, unchanged; raises PlanError if it is not there."""

    def ensure_image(self, target: Target, registry: dict, repository: str, tag: str, project: str) -> None:
        """Builds and pushes the image unless the registry already has that tag."""

    def deployer_principal_id(self) -> str:
        """The object ID of whoever this deployment signs in as, for modules that grant it a role."""

    def apply_indexes(self, target: Target, outputs: dict, definitions: list[Path]) -> None:
        """Creates or updates the search indexes the definitions describe on the deployed service."""

    def index_changes(self, target: Target, outputs: dict, definitions: list[Path]) -> list[str]:
        """How the deployed indexes differ from their definitions (empty: no drift)."""


def index_definitions(placement: Placement) -> list[Path]:
    """The search index definitions a service's deployment applies after the service itself (`indexes` in the catalog)."""
    return [DEPLOY / path for path in placement.service.get("indexes", [])]


def protects(env: str) -> bool:
    """Whether an environment's stateful resources are protected against deletion (P12): production only."""
    return env == "prod"


@dataclass
class Deployer:
    plan: Plan
    env: str
    product: str
    drivers: dict[str, Driver]
    log: Callable[[str], None] = print
    promote_from: str | None = None
    """An environment whose images to copy instead of building them (promotion: what reaches prod is what test ran)."""
    environ: Mapping[str, str] = field(default_factory=lambda: os.environ)

    def targets(self) -> list[Target]:
        return [Target(placement, self.env, self.product) for placement in self.plan.placements]

    def driver(self, target: Target) -> Driver:
        return self.drivers[target.placement.driver]

    def parameters(self, target: Target, outputs: dict[str, dict], sha: str | None) -> dict:
        placement = target.placement

        if uses_deployer(placement) and DEPLOYER not in outputs:
            outputs[DEPLOYER] = {"principalId": self.driver(target).deployer_principal_id()}

        parameters = {
            "name": target.name,
            "location": placement.region,
            "tags": target.tags,
            **environment_parameters(placement, self.env),
            **resolve_inputs(placement, outputs),
            **secure_inputs(placement, self.environ),
        }

        if placement.stateful:
            parameters["protect"] = protects(self.env)

        if placement.image:
            if not sha:
                raise PlanError([f"{placement.purpose}: deploying it needs the commit SHA its image is tagged with."])

            parameters["image"] = f"{placement.image['repository']}:{sha}"
            parameters["appSettings"] = app_settings(self.plan, outputs)

        return parameters

    def up(self, sha: str | None, smoke: bool = True) -> dict[str, dict]:
        """Deploys every resource in dependency order; a resource's image is built first if its tag is not there yet."""
        outputs: dict[str, dict] = {}

        for target in self.targets():
            placement = target.placement
            parameters = self.parameters(target, outputs, sha)

            if placement.image and sha:
                registry = outputs[placement.image["registry"]]

                if self.promote_from:
                    source = self.source_registry(placement)
                    self.log(f"image   {placement.image['repository']}:{sha} from {source} to {registry.get('loginServer')}")
                    self.driver(target).promote_image(target, registry, source, placement.image["repository"], sha)
                else:
                    self.log(f"image   {placement.image['repository']}:{sha} in {registry.get('loginServer')}")
                    self.driver(target).ensure_image(target, registry, placement.image["repository"], sha, placement.image["project"])

            self.log(f"deploy  {placement.purpose:<15} {placement.cloud}/{placement.region}  {target.name}")
            outputs[placement.purpose] = self.driver(target).deploy(target, parameters)

            if definitions := index_definitions(placement):
                self.log(f"indexes {placement.purpose:<15} {', '.join(path.stem for path in definitions)}")
                self.driver(target).apply_indexes(target, outputs[placement.purpose], definitions)

            # A job the catalog marks to run (the migration step) finishes before anything that depends on it deploys.
            if placement.service.get("run"):
                self.log(f"run     {placement.purpose:<15} {target.name}")
                self.driver(target).run(target, outputs[placement.purpose])

        if smoke:
            for target in self.targets():
                url = outputs[target.placement.purpose].get("url")
                if target.placement.image and url:
                    self.healthy(f"{url}/health")

        return outputs

    def source_registry(self, placement: Placement) -> str:
        """The name of the registry the image is promoted from: the same registry resource in the source environment."""
        registry = self.plan.by_purpose()[placement.image["registry"]]

        return resource_name(registry.purpose, self.promote_from, registry.region, registry.service["type"], registry.service.get("compact", False))

    def deployed_settings(self) -> dict[str, str]:
        """The app settings the deployed environment runs with, from its resources' current outputs."""
        outputs = {target.placement.purpose: current for target in self.targets() if (current := self.driver(target).outputs(target)) is not None}

        return app_settings(self.plan, outputs)

    def drift(self) -> dict[str, list[str]]:
        """What deploying the plan again would change, per resource, with each deployed image kept as it is."""
        outputs: dict[str, dict] = {}
        drifted: dict[str, list[str]] = {}

        for target in self.targets():
            current = self.driver(target).outputs(target)

            if current is None:
                drifted[target.placement.purpose] = ["not deployed"]
                continue

            deployed_image = current.get("image")
            sha = deployed_image.rsplit(":", 1)[1] if deployed_image else None
            changes = self.driver(target).changes(target, self.parameters(target, outputs, sha))

            if definitions := index_definitions(target.placement):
                changes = [*changes, *self.driver(target).index_changes(target, current, definitions)]

            if changes:
                drifted[target.placement.purpose] = changes

            outputs[target.placement.purpose] = current

        return drifted

    def down(self, include_stateful: bool = False) -> list[str]:
        """Removes the plan's resources in reverse order; stateful ones only when asked. Returns what was kept."""
        kept: list[str] = []
        removed: list[Target] = []

        for target in reversed(self.targets()):
            if target.placement.stateful and not include_stateful:
                self.log(f"keep    {target.placement.purpose:<15} stateful; tear down with --include-stateful")
                kept.append(target.placement.purpose)
                continue

            self.log(f"remove  {target.placement.purpose:<15} {target.name}")
            self.driver(target).remove(target)
            removed.append(target)

        for driver in self.plan.drivers():
            self.drivers[driver].finish_removal([target for target in removed if target.placement.driver == driver])

        return kept

    def healthy(self, url: str, attempts: int = 30, wait: float = 10) -> None:
        """Waits for the host to answer 200 on its health endpoint."""
        for attempt in range(attempts):
            try:
                with urllib.request.urlopen(url, timeout=10) as response:
                    if response.status == 200:
                        self.log(f"healthy {url}")
                        return
            except (urllib.error.URLError, TimeoutError):
                pass

            if attempt < attempts - 1:
                time.sleep(wait)

        raise PlanError([f"{url} did not answer healthy after {attempts} attempts."])
