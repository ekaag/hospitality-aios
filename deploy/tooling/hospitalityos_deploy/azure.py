"""The Azure driver: each resource is a deployment stack in its lifecycle group's resource group, deployed from its Bicep
module with the Azure CLI. Azure keeps what each stack deployed, so there is no state file, and deleting a stack removes
what it made."""

from __future__ import annotations

import base64
import json
import os
import shutil
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

from .deployer import Target
from .model import DEPLOY, PlanError, resource_name, tags

REPOSITORY_ROOT = DEPLOY.parent

# What-if reports these when nothing would change.
_UNCHANGED = {"NoChange", "Ignore", "Unsupported"}

# Azure AI Search's data-plane API, for the index definitions in deploy/azure/search/.
SEARCH_API_VERSION = "2025-09-01"
SEARCH_RESOURCE = "https://search.azure.com"

# A role granted in the same deployment takes a few minutes to apply; index writes wait for it this long.
_ROLE_WAIT_SECONDS = 600

# Container Apps jobs (the migration step), through ARM: no CLI extension needed.
JOBS_API_VERSION = "2024-03-01"
# The job's own replica timeout is 30 minutes; it is given a little longer to start.
_JOB_WAIT_SECONDS = 2100
_JOB_FAILED = {"Failed", "Stopped", "Degraded"}


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


def token_for(resource: str) -> str:
    """An access token for a resource, as the signed-in identity."""
    return az_json("account", "get-access-token", "--resource", resource)["accessToken"]


def object_id(token: str) -> str:
    """The `oid` claim of an access token: the signed-in user's or service principal's object ID, without Graph."""
    payload = token.split(".")[1]
    claims = json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))

    return claims["oid"]


def search_request(method: str, url: str, token: str, body: dict | None = None) -> tuple[int, dict | None]:
    """One call to a search service's data-plane API: its status and JSON body."""
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode("utf-8") if body is not None else None,
        method=method,
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
    )

    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            text = response.read().decode("utf-8")
            return response.status, json.loads(text) if text else None
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8")
        return error.code, json.loads(text) if text.startswith("{") else {"error": text}


def index_shape(definition: dict) -> list[tuple]:
    """What drift compares: each field's name, type, and vector width (the service adds defaults to the rest)."""
    return sorted((field["name"], field["type"], field.get("dimensions")) for field in definition.get("fields", []))


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

        # A deleted Azure OpenAI (Foundry) resource is kept for 48 days, holding its name and subdomain; it keeps no data
        # of its own, so it is always purged.
        if target.placement.service.get("type") == "oai":
            az("cognitiveservices", "account", "purge", "--name", target.name, "--resource-group", target.group, "--location", target.placement.region, check=False)

    def finish_removal(self, targets: list[Target]) -> None:
        for group in sorted({target.group for target in targets}):
            exists = az("group", "exists", "--name", group, check=False).stdout.strip() == "true"
            if exists and not az_json("resource", "list", "--resource-group", group):
                az("group", "delete", "--name", group, "--yes")

    def deployer_principal_id(self) -> str:
        return object_id(token_for("https://management.azure.com/"))

    def apply_indexes(self, target: Target, outputs: dict, definitions: list[Path]) -> None:
        endpoint = outputs["endpoint"].rstrip("/")
        deadline = time.monotonic() + _ROLE_WAIT_SECONDS

        for path in definitions:
            definition = json.loads(path.read_text(encoding="utf-8"))
            url = f"{endpoint}/indexes/{definition['name']}?api-version={SEARCH_API_VERSION}"

            while True:
                status, body = search_request("PUT", url, token_for(SEARCH_RESOURCE), definition)

                if status in (200, 201, 204):
                    break

                # The module has just granted this identity Search Service Contributor; until it applies, writes are refused.
                if status in (401, 403) and time.monotonic() < deadline:
                    time.sleep(20)
                    continue

                raise PlanError([f"{target.placement.purpose}: applying index {definition['name']} failed ({status}): {body}"])

    def index_changes(self, target: Target, outputs: dict, definitions: list[Path]) -> list[str]:
        endpoint = outputs["endpoint"].rstrip("/")
        token = token_for(SEARCH_RESOURCE)
        changes: list[str] = []

        for path in definitions:
            definition = json.loads(path.read_text(encoding="utf-8"))
            status, deployed = search_request("GET", f"{endpoint}/indexes/{definition['name']}?api-version={SEARCH_API_VERSION}", token)

            if status == 404:
                changes.append(f"Create: index {definition['name']}")
            elif status != 200:
                changes.append(f"Unknown: index {definition['name']} could not be read ({status})")
            elif index_shape(deployed or {}) != index_shape(definition):
                changes.append(f"Modify: index {definition['name']} fields")

        return changes

    def run(self, target: Target, outputs: dict) -> None:
        job = f"https://management.azure.com{outputs['jobId']}"
        started = az_json("rest", "--method", "post", "--url", f"{job}/start?api-version={JOBS_API_VERSION}") or {}
        execution = started.get("name")

        if not execution:
            raise PlanError([f"{target.placement.purpose}: starting job {target.name} returned no execution to follow."])

        deadline = time.monotonic() + _JOB_WAIT_SECONDS

        while True:
            status = (az_json("rest", "--method", "get", "--url", f"{job}/executions/{execution}?api-version={JOBS_API_VERSION}") or {}).get("properties", {}).get("status")

            if status == "Succeeded":
                return

            if status in _JOB_FAILED or time.monotonic() > deadline:
                raise PlanError([
                    f"{target.placement.purpose}: job {target.name} execution {execution} {(status or 'did not finish').lower()}; "
                    "its output is in the logs workspace (ContainerAppConsoleLogs_CL)."
                ])

            time.sleep(10)

    def promote_image(self, target: Target, registry: dict, source_registry: str, repository: str, tag: str) -> None:
        name = registry["registryName"]

        if tag in (az_json("acr", "repository", "show-tags", "--name", name, "--repository", repository, check=False) or []):
            return

        source = az_json("acr", "show", "--name", source_registry, check=False)
        if not source or tag not in (az_json("acr", "repository", "show-tags", "--name", source_registry, "--repository", repository, check=False) or []):
            raise PlanError([f"{target.placement.purpose}: {repository}:{tag} is not in {source_registry}, so it has not been deployed there to promote."])

        az("acr", "import", "--name", name, "--source", f"{repository}:{tag}", "--registry", source["id"], "--image", f"{repository}:{tag}")

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

