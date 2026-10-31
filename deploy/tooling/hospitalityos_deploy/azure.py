"""The Azure driver: each resource is a deployment stack in its lifecycle group's resource group, deployed from its Bicep
module with the Azure CLI. Azure keeps what each stack deployed, so there is no state file, and deleting a stack removes
what it made."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
from .deployer import Target
from .model import DEPLOY, PlanError, resource_name, tags

REPOSITORY_ROOT = DEPLOY.parent

# What-if reports these when nothing would change.
_UNCHANGED = {"NoChange", "Ignore", "Unsupported"}


def az(*arguments: str, check: bool = True) -> subprocess.CompletedProcess:
    """Runs the Azure CLI (az.cmd on Windows, found through PATHEXT)."""
    result = subprocess.run([shutil.which("az") or "az", *arguments], capture_output=True, text=True)

    if check and result.returncode != 0:
        raise PlanError([f"az {' '.join(arguments[:3])} failed: {result.stderr.strip() or result.stdout.strip()}"])

    return result


def az_json(*arguments: str, check: bool = True):
    result = az(*arguments, "--output", "json", check=check)

    return json.loads(result.stdout) if result.returncode == 0 and result.stdout.strip() else None


def parameters_file(parameters: dict) -> str:
    """An ARM parameters file for the module, written to a temporary file the CLI reads with @path."""
    document = {
        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
        "contentVersion": "1.0.0.0",
        "parameters": {key: {"value": value} for key, value in parameters.items()},
    }
    handle, path = tempfile.mkstemp(suffix=".json", prefix="hospitalityos-")

    with os.fdopen(handle, "w", encoding="utf-8") as file:
        json.dump(document, file)

    return path


def outputs_of(stack: dict) -> dict:
    """A stack's outputs as plain values."""
    return {key: value.get("value") for key, value in (stack.get("outputs") or {}).items()}


class AzureDriver:
    def deploy(self, target: Target, parameters: dict) -> dict:
        placement = target.placement
        az("group", "create", "--name", target.group, "--location", placement.region, "--tags", "product=" + target.product, "env=" + target.env)
        path = parameters_file(parameters)

        try:
            stack = az_json(
                "stack", "group", "create",
                "--name", target.stack,
                "--resource-group", target.group,
                "--template-file", str(placement.module),
                "--parameters", f"@{path}",
                "--action-on-unmanage", "deleteResources",
                "--deny-settings-mode", "none",
                "--yes",
            )
        finally:
            os.remove(path)

        return outputs_of(stack or {})

    def outputs(self, target: Target) -> dict | None:
        stack = az_json("stack", "group", "show", "--name", target.stack, "--resource-group", target.group, check=False)

        return outputs_of(stack) if stack else None

    def changes(self, target: Target, parameters: dict) -> list[str]:
        path = parameters_file(parameters)

        try:
            report = az_json(
                "deployment", "group", "what-if",
                "--resource-group", target.group,
                "--template-file", str(target.placement.module),
                "--parameters", f"@{path}",
                "--result-format", "ResourceIdOnly",
                "--no-pretty-print",
            )
        finally:
            os.remove(path)

        return [
            f"{change.get('changeType')}: {change.get('resourceId')}"
            for change in (report or {}).get("changes", [])
            if change.get("changeType") not in _UNCHANGED
        ]

    def remove(self, target: Target) -> None:
        if self.outputs(target) is None:
            return

        az("stack", "group", "delete", "--name", target.stack, "--resource-group", target.group, "--action-on-unmanage", "deleteResources", "--yes")

        # A deleted vault stays soft-deleted, keeping its name; unprotected (non-production) ones are purged so the
        # environment can be built again under the same names.
        if target.placement.service.get("type") == "kv":
            deleted = az_json("keyvault", "list-deleted", "--query", f"[?name=='{target.name}']", check=False) or []
            if deleted and not deleted[0].get("properties", {}).get("purgeProtectionEnabled"):
                az("keyvault", "purge", "--name", target.name, "--location", target.placement.region)

    def finish_removal(self, targets: list[Target]) -> None:
        for group in sorted({target.group for target in targets}):
            exists = az("group", "exists", "--name", group, check=False).stdout.strip() == "true"
            if exists and not az_json("resource", "list", "--resource-group", group):
                az("group", "delete", "--name", group, "--yes")

    def ensure_image(self, target: Target, registry: dict, repository: str, tag: str, project: str) -> None:
        name, server = registry["registryName"], registry["loginServer"]
        existing = az_json("acr", "repository", "show-tags", "--name", name, "--repository", repository, check=False) or []

        if tag in existing:
            return

        # A registry token instead of `az acr login`, so pushing needs no Docker: the SDK pushes the image itself.
        token = az_json("acr", "login", "--name", name, "--expose-token")["accessToken"]
        environment = {
            **os.environ,
            "DOTNET_CONTAINER_REGISTRY_UNAME": "00000000-0000-0000-0000-000000000000",
            "DOTNET_CONTAINER_REGISTRY_PWORD": token,
        }
        result = subprocess.run(
            [
                "dotnet", "publish", str(REPOSITORY_ROOT / project), "--configuration", "Release",
                "-t:PublishContainer",
                f"-p:ContainerRegistry={server}",
                f"-p:ContainerRepository={repository}",
                f"-p:ContainerImageTags={tag}",
                "-p:ContainerFamily=noble-chiseled",
            ],
            env=environment,
            capture_output=True,
            text=True,
        )

        if result.returncode != 0:
            raise PlanError([f"Building the {repository} image failed:\n{result.stdout[-4000:]}{result.stderr[-2000:]}"])


# The bootstrap: one subscription deployment stack holding the GitHub deployment identity.
BOOTSTRAP_STACK = "hospitalityos-deploy"


def bootstrap(product: str, region: str, repository: str, environments: list[str]) -> dict:
    """Deploys the deployment identity and its OIDC trust; returns the values GitHub Actions signs in with."""
    identity = resource_name("deploy", "shared", region, "id")
    parameters = {
        "groupName": f"rg-{product}-deploy",
        "identityName": identity,
        "location": region,
        "tags": tags(product, "deploy", "shared", identity),
        "repository": repository,
        "environments": environments,
    }
    path = parameters_file(parameters)

    try:
        stack = az_json(
            "stack", "sub", "create",
            "--name", BOOTSTRAP_STACK,
            "--location", region,
            "--template-file", str(DEPLOY / "azure" / "bootstrap" / "main.bicep"),
            "--parameters", f"@{path}",
            "--action-on-unmanage", "deleteAll",
            "--deny-settings-mode", "none",
            "--yes",
        )
    finally:
        os.remove(path)

    values = outputs_of(stack or {})

    return {"AZURE_CLIENT_ID": values["clientId"], "AZURE_TENANT_ID": values["tenantId"], "AZURE_SUBSCRIPTION_ID": values["subscriptionId"]}


def bootstrap_down() -> None:
    """Removes the deployment identity, its trust, and its role assignments."""
    if az_json("stack", "sub", "show", "--name", BOOTSTRAP_STACK, check=False):
        az("stack", "sub", "delete", "--name", BOOTSTRAP_STACK, "--action-on-unmanage", "deleteAll", "--yes")


def signed_in() -> dict | None:
    """The subscription the Azure CLI is signed in to, or None."""
    return az_json("account", "show", check=False)

