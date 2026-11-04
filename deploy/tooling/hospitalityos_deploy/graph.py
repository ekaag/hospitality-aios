"""The Microsoft Graph driver (K4b): deploys what lives in an environment's Microsoft Entra External ID tenant (the app
registrations, their consent, and the user flow staff sign in through) from a definition such as
deploy/azure/signin/apps.json. Bicep's Graph extension cannot be deployed as a deployment stack, and an external tenant
has no subscription to deploy into, so the driver calls Graph itself: every call is idempotent, apps are upserted by
their unique names, and removing deletes them for good. It signs in to the external tenant as that tenant's own
deployment app (`bootstrap-signin` creates it): with GitHub's OIDC token in workflows, or the Azure CLI locally."""

from __future__ import annotations

import json
import os
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from .deployer import Target
from .model import DEPLOY, PlanError, group_name, resource_name, tags

GRAPH = "https://graph.microsoft.com/v1.0"
GRAPH_APP_ID = "00000003-0000-0000-c000-000000000000"

# The delegated Graph permissions every sign-in needs, and the application permissions the tenant's deployment app holds.
SIGN_IN_SCOPES = ("openid", "offline_access")
DEPLOYMENT_ROLES = (
    "Application.ReadWrite.OwnedBy",
    "DelegatedPermissionGrant.ReadWrite.All",
    "AppRoleAssignment.ReadWrite.All",
    "EventListener.ReadWrite.All",
    "User.Read.All",
)

# Stable IDs for scopes and app roles, so deploying again changes nothing.
_NAMESPACE = uuid.UUID("5b6f2f8e-0a7b-4f4e-9d0c-6a1b8f0c4d21")

Send = Callable[[str, str, dict | None, dict[str, str]], tuple[int, dict | None]]


def stable_id(*parts: str) -> str:
    return str(uuid.uuid5(_NAMESPACE, ":".join(parts)))


def http_send(method: str, url: str, body: dict | None, headers: dict[str, str]) -> tuple[int, dict | None]:
    """One HTTPS call with a JSON body: its status and JSON body."""
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(url, data=data, method=method, headers={"Content-Type": "application/json", **headers})

    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            text = response.read().decode("utf-8")
            return response.status, json.loads(text) if text.strip() else None
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8")
        return error.code, json.loads(text) if text.strip() else None


@dataclass(frozen=True)
class SignInTenant:
    """An environment's External ID tenant: its ID, its domain prefix (`<prefix>.ciamlogin.com`), and the client ID of
    the deployment app inside it."""

    tenant_id: str
    domain: str
    client_id: str

    @property
    def issuer(self) -> str:
        return f"https://{self.tenant_id}.ciamlogin.com/{self.tenant_id}/v2.0"

    @property
    def authority(self) -> str:
        """Where the API reads the tenant's signing keys and issuer (it appends the OpenID configuration path)."""
        return f"https://{self.domain}.ciamlogin.com/{self.tenant_id}/v2.0"

    @property
    def spa_authority(self) -> str:
        """Where the web apps send people to sign in (MSAL)."""
        return f"https://{self.domain}.ciamlogin.com/{self.tenant_id}"

    @staticmethod
    def from_environment(environ: dict[str, str] | None = None) -> SignInTenant:
        environ = dict(os.environ) if environ is None else environ
        names = ("HOSPITALITYOS_SIGNIN_TENANT_ID", "HOSPITALITYOS_SIGNIN_DOMAIN", "HOSPITALITYOS_SIGNIN_CLIENT_ID")
        missing = [name for name in names if not environ.get(name)]

        if missing:
            raise PlanError([f"signin: set {', '.join(missing)} (from deploy bootstrap-signin) to deploy into the External ID tenant."])

        return SignInTenant(*(environ[name] for name in names))


def graph_token(tenant: SignInTenant, environ: dict[str, str] | None = None, send: Send = http_send) -> str:
    """A Graph token in the external tenant: in GitHub Actions, the job's OIDC token exchanged as the tenant's deployment
    app (federated credential, no secret); otherwise the Azure CLI's sign-in to that tenant."""
    environ = dict(os.environ) if environ is None else environ

    if request_url := environ.get("ACTIONS_ID_TOKEN_REQUEST_URL"):
        _, github = send("GET", f"{request_url}&audience=api://AzureADTokenExchange", None, {"Authorization": f"Bearer {environ['ACTIONS_ID_TOKEN_REQUEST_TOKEN']}"})
        form = urllib.parse.urlencode({
            "client_id": tenant.client_id,
            "scope": "https://graph.microsoft.com/.default",
            "grant_type": "client_credentials",
            "client_assertion_type": "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            "client_assertion": (github or {})["value"],
        }).encode("utf-8")
        request = urllib.request.Request(f"https://login.microsoftonline.com/{tenant.tenant_id}/oauth2/v2.0/token", data=form, method="POST")

        with urllib.request.urlopen(request, timeout=60) as response:
            return json.loads(response.read().decode("utf-8"))["access_token"]

    return az_graph_token(tenant.tenant_id)


def az_graph_token(tenant_id: str) -> str:
    from .azure import az_json

    return az_json("account", "get-access-token", "--resource-type", "ms-graph", "--tenant", tenant_id)["accessToken"]


class Graph:
    """Microsoft Graph calls in one tenant."""

    def __init__(self, token: Callable[[], str], send: Send = http_send):
        self.token = token
        self.send = send

    def call(self, method: str, path: str, body: dict | None = None, headers: dict[str, str] | None = None) -> tuple[int, dict | None]:
        return self.send(method, f"{GRAPH}{path}", body, {"Authorization": f"Bearer {self.token()}", **(headers or {})})

    def get(self, path: str) -> dict | None:
        status, body = self.call("GET", path)

        if status == 404:
            return None

        if status != 200:
            raise PlanError([f"GET {path} failed ({status}): {body}"])

        return body

    def require(self, method: str, path: str, body: dict | None = None, headers: dict[str, str] | None = None) -> dict | None:
        status, answer = self.call(method, path, body, headers)

        if status not in (200, 201, 204):
            raise PlanError([f"{method} {path} failed ({status}): {answer}"])

        return answer

    def app(self, unique_name: str) -> dict | None:
        return self.get(f"/applications(uniqueName='{unique_name}')")

    def upsert_app(self, unique_name: str, body: dict) -> dict:
        self.require("PATCH", f"/applications(uniqueName='{unique_name}')", body, {"Prefer": "create-if-missing"})
        app = self.app(unique_name)

        if app is None:
            raise PlanError([f"The app {unique_name} was not found after it was written."])

        return app

    def service_principal(self, app_id: str, create: bool = True) -> dict | None:
        found = self.get(f"/servicePrincipals(appId='{app_id}')")

        if found is None and create:
            found = self.require("POST", "/servicePrincipals", {"appId": app_id})

        return found

    def grant(self, client_id: str, resource_id: str, scope: str) -> None:
        """Consent for every user of the tenant to `scope` (space-separated) of the resource, so sign-in asks nobody."""
        filter_ = urllib.parse.quote(f"clientId eq '{client_id}' and resourceId eq '{resource_id}' and consentType eq 'AllPrincipals'")
        existing = (self.get(f"/oauth2PermissionGrants?$filter={filter_}") or {}).get("value", [])

        if existing:
            if existing[0].get("scope", "").split() != scope.split():
                self.require("PATCH", f"/oauth2PermissionGrants/{existing[0]['id']}", {"scope": scope})
            return

        self.require("POST", "/oauth2PermissionGrants", {"clientId": client_id, "consentType": "AllPrincipals", "resourceId": resource_id, "scope": scope})

    def flow(self, display_name: str) -> dict | None:
        filter_ = urllib.parse.quote(f"displayName eq '{display_name}'")
        flows = (self.get(f"/identity/authenticationEventsFlows?$filter={filter_}") or {}).get("value", [])
        return flows[0] if flows else None

    def flow_apps(self, flow_id: str) -> list[str]:
        linked = self.get(f"/identity/authenticationEventsFlows/{flow_id}/conditions/applications/includeApplications") or {}
        return [entry["appId"] for entry in linked.get("value", [])]

    def delete_app(self, unique_name: str) -> None:
        """Deletes an app for good (not to the recycle bin), so its unique name is free again."""
        app = self.app(unique_name)

        if app is None:
            return

        self.require("DELETE", f"/applications/{app['id']}")

        for _ in range(10):
            status, _ = self.call("DELETE", f"/directory/deletedItems/{app['id']}")
            if status in (204, 404):
                return
            time.sleep(3)


# What every deployment outputs, besides each single-page app's client ID under its `output` name.
OUTPUTS = ("tenantId", "issuer", "authority", "spaAuthority", "apiClientId", "scope")


def load_definition(placement_module: Path) -> dict:
    return json.loads(placement_module.read_text(encoding="utf-8"))


def declared_outputs(definition: dict) -> set[str]:
    return {*OUTPUTS, *(spa["output"] for spa in definition["spas"])}


def desired(definition: dict, name: str, env: str) -> dict:
    """What the tenant should hold for this environment, as Graph bodies, before IDs only Graph knows are filled in."""
    api = definition["api"]
    scope = api["scope"]
    spas = []

    for spa in definition["spas"]:
        spas.append({
            "uniqueName": f"{name}-{spa['suffix']}",
            "output": spa["output"],
            "body": {
                "displayName": f"{spa['displayName']} ({env})",
                "signInAudience": "AzureADMyOrg",
                "spa": {"redirectUris": spa["redirectUris"].get(env, [])},
            },
        })

    return {
        "api": {
            "uniqueName": f"{name}-{api['suffix']}",
            "scopeId": stable_id(name, "scope", scope["value"]),
            "scope": scope["value"],
            "body": {
                "displayName": f"{api['displayName']} ({env})",
                "signInAudience": "AzureADMyOrg",
                "api": {
                    "requestedAccessTokenVersion": 2,
                    "oauth2PermissionScopes": [{
                        "id": stable_id(name, "scope", scope["value"]),
                        "value": scope["value"],
                        "type": "User",
                        "isEnabled": True,
                        "adminConsentDisplayName": scope["displayName"],
                        "adminConsentDescription": scope["displayName"],
                        "userConsentDisplayName": scope["displayName"],
                        "userConsentDescription": scope["displayName"],
                    }],
                },
                "appRoles": [{
                    "id": stable_id(name, "role", role["value"]),
                    "value": role["value"],
                    "displayName": role["displayName"],
                    "description": role["description"],
                    "allowedMemberTypes": ["User"],
                    "isEnabled": True,
                } for role in api.get("appRoles", [])],
            },
        },
        "spas": spas,
        "flow": {
            "displayName": name,
            "body": {
                "@odata.type": "#microsoft.graph.externalUsersSelfServiceSignUpEventsFlow",
                "displayName": name,
                "onInteractiveAuthFlowStart": {
                    "@odata.type": "#microsoft.graph.onInteractiveAuthFlowStartExternalUsersSelfServiceSignUp",
                    "isSignUpAllowed": True,
                },
                "onAuthenticationMethodLoadStart": {
                    "@odata.type": "#microsoft.graph.onAuthenticationMethodLoadStartExternalUsersSelfServiceSignUp",
                    "identityProviders": [{"id": provider} for provider in definition["userFlow"]["identityProviders"]],
                },
                "onAttributeCollection": {
                    "@odata.type": "#microsoft.graph.onAttributeCollectionExternalUsersSelfServiceSignUp",
                    "attributes": [{"id": attribute} for attribute in definition["userFlow"]["attributes"]],
                },
            },
        },
    }


class GraphDriver:
    """Deploys a Graph definition into the environment's External ID tenant; see the module's description."""

    def __init__(self, tenant: SignInTenant | None = None, graph: Graph | None = None):
        self._tenant = tenant
        self._graph = graph

    @property
    def tenant(self) -> SignInTenant:
        if self._tenant is None:
            self._tenant = SignInTenant.from_environment()
        return self._tenant

    @property
    def graph(self) -> Graph:
        if self._graph is None:
            tenant = self.tenant
            cached: dict[str, str] = {}
            self._graph = Graph(lambda: cached.setdefault("token", graph_token(tenant)))
        return self._graph

    def deploy(self, target: Target, parameters: dict) -> dict:
        want = desired(load_definition(target.placement.module), target.name, target.env)
        graph = self.graph
        graph_sp = graph.service_principal(GRAPH_APP_ID, create=False) or {}
        sign_in_scopes = {scope["value"]: scope["id"] for scope in graph_sp.get("oauth2PermissionScopes", [])}

        api = graph.upsert_app(want["api"]["uniqueName"], want["api"]["body"])
        graph.require("PATCH", f"/applications/{api['id']}", {"identifierUris": [f"api://{api['appId']}"]})
        api_sp = graph.service_principal(api["appId"])

        for spa in want["spas"]:
            body = {
                **spa["body"],
                "requiredResourceAccess": [
                    {"resourceAppId": api["appId"], "resourceAccess": [{"id": want["api"]["scopeId"], "type": "Scope"}]},
                    {"resourceAppId": GRAPH_APP_ID, "resourceAccess": [{"id": sign_in_scopes[scope], "type": "Scope"} for scope in SIGN_IN_SCOPES]},
                ],
            }
            app = graph.upsert_app(spa["uniqueName"], body)
            spa_sp = graph.service_principal(app["appId"])
            graph.grant(spa_sp["id"], api_sp["id"], want["api"]["scope"])
            graph.grant(spa_sp["id"], graph_sp["id"], " ".join(SIGN_IN_SCOPES))
            spa["appId"] = app["appId"]

        flow = graph.flow(want["flow"]["displayName"]) or graph.require("POST", "/identity/authenticationEventsFlows", want["flow"]["body"])
        linked = graph.flow_apps(flow["id"])

        for spa in want["spas"]:
            if spa["appId"] not in linked:
                graph.require(
                    "POST",
                    f"/identity/authenticationEventsFlows/{flow['id']}/conditions/applications/includeApplications",
                    {"@odata.type": "#microsoft.graph.authenticationConditionApplication", "appId": spa["appId"]},
                )

        return self._outputs(api["appId"], want, {spa["output"]: spa["appId"] for spa in want["spas"]})

    def outputs(self, target: Target) -> dict | None:
        want = desired(load_definition(target.placement.module), target.name, target.env)
        api = self.graph.app(want["api"]["uniqueName"])

        if api is None:
            return None

        spas = {spa["output"]: (self.graph.app(spa["uniqueName"]) or {}).get("appId", "") for spa in want["spas"]}
        return self._outputs(api["appId"], want, spas)

    def changes(self, target: Target, parameters: dict) -> list[str]:
        want = desired(load_definition(target.placement.module), target.name, target.env)
        graph = self.graph
        changes: list[str] = []
        api = graph.app(want["api"]["uniqueName"])

        if api is None:
            return [f"app {want['api']['uniqueName']} is missing"]

        if sorted(scope["value"] for scope in api.get("api", {}).get("oauth2PermissionScopes", [])) != [want["api"]["scope"]]:
            changes.append(f"app {want['api']['uniqueName']}: scopes differ")

        if sorted(role["value"] for role in api.get("appRoles", [])) != sorted(role["value"] for role in want["api"]["body"]["appRoles"]):
            changes.append(f"app {want['api']['uniqueName']}: app roles differ")

        if api.get("identifierUris") != [f"api://{api['appId']}"]:
            changes.append(f"app {want['api']['uniqueName']}: identifier URI differs")

        flow = graph.flow(want["flow"]["displayName"])
        linked = graph.flow_apps(flow["id"]) if flow else []

        if flow is None:
            changes.append(f"user flow {want['flow']['displayName']} is missing")

        for spa in want["spas"]:
            app = graph.app(spa["uniqueName"])

            if app is None:
                changes.append(f"app {spa['uniqueName']} is missing")
                continue

            if sorted(app.get("spa", {}).get("redirectUris", [])) != sorted(spa["body"]["spa"]["redirectUris"]):
                changes.append(f"app {spa['uniqueName']}: redirect URIs differ")

            if app.get("displayName") != spa["body"]["displayName"]:
                changes.append(f"app {spa['uniqueName']}: display name differs")

            if flow and app["appId"] not in linked:
                changes.append(f"app {spa['uniqueName']}: not on the user flow")

        return changes

    def remove(self, target: Target) -> None:
        want = desired(load_definition(target.placement.module), target.name, target.env)
        graph = self.graph

        if flow := graph.flow(want["flow"]["displayName"]):
            graph.require("DELETE", f"/identity/authenticationEventsFlows/{flow['id']}")

        for spa in want["spas"]:
            graph.delete_app(spa["uniqueName"])

        graph.delete_app(want["api"]["uniqueName"])

    def finish_removal(self, targets: list[Target]) -> None:
        """Graph leaves nothing behind once the apps are deleted for good."""

    def deployer_principal_id(self) -> str:
        raise PlanError(["The graph driver grants nothing to whoever deploys."])

    def apply_indexes(self, target: Target, outputs: dict, definitions: list[Path]) -> None:
        raise PlanError([f"{target.placement.purpose}: the graph driver has no search indexes."])

    def index_changes(self, target: Target, outputs: dict, definitions: list[Path]) -> list[str]:
        return []

    def ensure_image(self, target: Target, registry: dict, repository: str, tag: str, project: str) -> None:
        raise PlanError([f"{target.placement.purpose}: the graph driver builds no images."])

    def _outputs(self, api_app_id: str, want: dict, spas: dict[str, str]) -> dict:
        tenant = self.tenant
        return {
            "tenantId": tenant.tenant_id,
            "issuer": tenant.issuer,
            "authority": tenant.authority,
            "spaAuthority": tenant.spa_authority,
            "apiClientId": api_app_id,
            "scope": f"api://{api_app_id}/{want['api']['scope']}",
            **spas,
        }


def operator_assignment(graph: Graph, api_unique_name: str, email: str) -> tuple[dict, dict, str]:
    """The API's service principal, the user signed up with `email`, and the Operator role's ID."""
    api = graph.app(api_unique_name)

    if api is None:
        raise PlanError([f"The app {api_unique_name} is not deployed; deploy the plan first."])

    role = next((role["id"] for role in api.get("appRoles", []) if role["value"] == "Operator"), None)
    api_sp = graph.service_principal(api["appId"], create=False)
    domain = next(domain["name"] for domain in (graph.get("/organization?$select=verifiedDomains") or {"value": [{"verifiedDomains": []}]})["value"][0]["verifiedDomains"] if domain.get("isInitial"))
    filter_ = urllib.parse.quote(f"identities/any(identity:identity/issuerAssignedId eq '{email}' and identity/issuer eq '{domain}')")
    users = (graph.get(f"/users?$filter={filter_}&$select=id,displayName") or {}).get("value", [])

    if role is None or api_sp is None:
        raise PlanError([f"The app {api_unique_name} has no Operator role; deploy the plan again."])

    if not users:
        raise PlanError([f"Nobody has signed up with {email} yet; they sign in to a staff app once first."])

    return api_sp, users[0], role


def grant_operator(graph: Graph, api_unique_name: str, email: str) -> str:
    api_sp, user, role = operator_assignment(graph, api_unique_name, email)
    assigned = (graph.get(f"/servicePrincipals/{api_sp['id']}/appRoleAssignedTo") or {}).get("value", [])

    if not any(entry["principalId"] == user["id"] and entry["appRoleId"] == role for entry in assigned):
        graph.require("POST", f"/servicePrincipals/{api_sp['id']}/appRoleAssignedTo", {"principalId": user["id"], "resourceId": api_sp["id"], "appRoleId": role})

    return user.get("displayName") or email


def revoke_operator(graph: Graph, api_unique_name: str, email: str) -> str:
    api_sp, user, role = operator_assignment(graph, api_unique_name, email)

    for entry in (graph.get(f"/servicePrincipals/{api_sp['id']}/appRoleAssignedTo") or {}).get("value", []):
        if entry["principalId"] == user["id"] and entry["appRoleId"] == role:
            graph.require("DELETE", f"/servicePrincipals/{api_sp['id']}/appRoleAssignedTo/{entry['id']}")

    return user.get("displayName") or email


def tenant_login(tenant_id: str, device_code: bool = False) -> list[str]:
    """The Azure CLI sign-in to a tenant that has no subscription: in a browser, or with a device code where there is
    none (the bootstrap workflow prints the code in its log)."""
    return [*_az(), "login", "--tenant", tenant_id, "--allow-no-subscriptions", *(["--use-device-code"] if device_code else [])]


def bootstrap_signin(product: str, env: str, region: str, domain: str, country: str, data_location: str, repository: str, device_code: bool = False) -> dict[str, str]:
    """Creates the environment's External ID tenant (once; it can take half an hour), then, signed in to it as its
    administrator, the deployment app CI signs in to it as: trusted for the repository's workflows like the Azure
    deployment identity, holding only the Graph permissions the driver needs. Returns the values the workflows need.
    The Azure CLI is left on the subscription it started on, so the next environment can be bootstrapped after it."""
    from .azure import az, az_json

    subscription = az_json("account", "show")["id"]

    try:
        return _bootstrap_signin(product, env, region, domain, country, data_location, repository, device_code)
    finally:
        az("account", "set", "--subscription", subscription)


def _bootstrap_signin(product: str, env: str, region: str, domain: str, country: str, data_location: str, repository: str, device_code: bool) -> dict[str, str]:
    from .azure import az, az_json, outputs_of, parameters_file

    group = group_name(product, "signin", env)
    directory = resource_name("signin", env, region, "ciam")
    az("group", "create", "--name", group, "--location", region, "--tags", *[f"{key}={value}" for key, value in tags(product, "signin", env, directory).items()])
    path = parameters_file({"domainPrefix": domain, "countryCode": country, "location": data_location, "tags": tags(product, "signin", env, directory)})

    try:
        deployment = az_json(
            "deployment", "group", "create",
            "--resource-group", group,
            "--name", directory,
            "--template-file", str(DEPLOY / "azure" / "bootstrap" / "signin.bicep"),
            "--parameters", f"@{path}",
        )
    finally:
        os.remove(path)

    tenant_id = outputs_of((deployment or {}).get("properties", {}))["tenantId"]
    print(f"Sign in to the new tenant {domain}.onmicrosoft.com ({tenant_id}) as its administrator.")
    subprocess.run(tenant_login(tenant_id, device_code), check=True)

    graph = Graph(lambda: az_graph_token(tenant_id))
    unique_name = resource_name("deploy", env, region, "app")
    app = graph.upsert_app(unique_name, {"displayName": f"HospitalityOS deployment ({env})", "signInAudience": "AzureADMyOrg"})
    sp = graph.service_principal(app["appId"])

    subjects = [f"repo:{repository}:ref:refs/heads/main", f"repo:{repository}:pull_request", f"repo:{repository}:environment:{env}"]
    existing = {credential["subject"] for credential in (graph.get(f"/applications/{app['id']}/federatedIdentityCredentials") or {}).get("value", [])}

    for subject in subjects:
        if subject not in existing:
            graph.require("POST", f"/applications/{app['id']}/federatedIdentityCredentials", {
                "name": f"github-{stable_id(subject)[:8]}",
                "issuer": "https://token.actions.githubusercontent.com",
                "subject": subject,
                "audiences": ["api://AzureADTokenExchange"],
            })

    graph_sp = graph.service_principal(GRAPH_APP_ID, create=False) or {}
    roles = {role["value"]: role["id"] for role in graph_sp.get("appRoles", [])}
    held = {entry["appRoleId"] for entry in (graph.get(f"/servicePrincipals/{sp['id']}/appRoleAssignments") or {}).get("value", [])}

    for permission in DEPLOYMENT_ROLES:
        if roles[permission] not in held:
            graph.require("POST", f"/servicePrincipals/{sp['id']}/appRoleAssignments", {"principalId": sp["id"], "resourceId": graph_sp["id"], "appRoleId": roles[permission]})

    return {"HOSPITALITYOS_SIGNIN_TENANT_ID": tenant_id, "HOSPITALITYOS_SIGNIN_DOMAIN": domain, "HOSPITALITYOS_SIGNIN_CLIENT_ID": app["appId"]}


def github_variables(env: str) -> dict[str, str]:
    """The repository variables the workflows map onto the driver's settings for an environment, e.g.
    `SIGNIN_TENANT_ID_DEV`: per environment, but not environment-scoped, so drift checks and pull request previews (which
    run outside the environment) read them too."""
    return {name: f"{name.removeprefix('HOSPITALITYOS_')}_{env.upper()}" for name in ("HOSPITALITYOS_SIGNIN_TENANT_ID", "HOSPITALITYOS_SIGNIN_DOMAIN", "HOSPITALITYOS_SIGNIN_CLIENT_ID")}


def bootstrap_signin_down(product: str, env: str, region: str, tenant_id: str) -> None:
    """Removes the deployment app from the tenant, then the tenant. Azure refuses to delete a tenant that still has
    users or apps: tear the signin resource down first, and remove staff accounts in the Entra admin center."""
    from .azure import az, az_json

    subprocess.run([*_az(), "login", "--tenant", tenant_id, "--allow-no-subscriptions"], check=True)
    Graph(lambda: az_graph_token(tenant_id)).delete_app(resource_name("deploy", env, region, "app"))
    group = group_name(product, "signin", env)

    if az_json("group", "show", "--name", group, check=False):
        az("group", "delete", "--name", group, "--yes")


def _az() -> list[str]:
    import shutil

    return [shutil.which("az") or "az"]
