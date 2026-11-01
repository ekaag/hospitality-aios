"""The Microsoft Graph driver against a stand-in Graph that keeps a tenant's directory in memory."""

import re
import unittest
import uuid
from urllib.parse import unquote

from hospitalityos_deploy.deployer import Deployer, Target
from hospitalityos_deploy.graph import GRAPH, GRAPH_APP_ID, Graph, GraphDriver, SignInTenant, grant_operator, graph_token, revoke_operator
from hospitalityos_deploy.model import DEPLOY, Placement, Plan, PlanError

TENANT = SignInTenant("tenant-1", "hospitalityosdev", "deploy-client")


class FakeGraph:
    """Enough of Graph for the driver: applications by unique name, service principals, consent grants, user flows,
    users, and app role assignments. Records every write."""

    def __init__(self):
        self.apps: dict[str, dict] = {}
        self.sps: dict[str, dict] = {
            GRAPH_APP_ID: {
                "id": "sp-graph",
                "appId": GRAPH_APP_ID,
                "oauth2PermissionScopes": [{"value": "openid", "id": "scope-openid"}, {"value": "offline_access", "id": "scope-offline"}],
            }
        }
        self.grants: list[dict] = []
        self.flows: list[dict] = []
        self.flow_apps: dict[str, list[str]] = {}
        self.users = [{"id": "user-ops", "displayName": "Ops Person", "identities": [{"issuer": "hospitalityosdev.onmicrosoft.com", "issuerAssignedId": "ops@example.com"}]}]
        self.assigned: list[dict] = []
        self.deleted: list[str] = []
        self.writes: list[tuple[str, str]] = []

    def send(self, method: str, url: str, body, headers) -> tuple[int, dict | None]:
        assert headers["Authorization"] == "Bearer token"
        path = unquote(url.removeprefix(GRAPH))

        if method != "GET":
            self.writes.append((method, re.sub(r"[0-9a-f-]{36}", "<id>", path)))

        if match := re.fullmatch(r"/applications\(uniqueName='([^']+)'\)", path):
            name = match.group(1)
            if method == "GET":
                return (200, self.apps[name]) if name in self.apps else (404, None)
            assert headers.get("Prefer") == "create-if-missing"
            app = self.apps.setdefault(name, {"id": str(uuid.uuid4()), "appId": str(uuid.uuid4()), "uniqueName": name, "identifierUris": []})
            app.update(body)
            return 204, None

        if match := re.fullmatch(r"/applications/([^/]+)", path):
            app = next(app for app in self.apps.values() if app["id"] == match.group(1))
            if method == "PATCH":
                app.update(body)
            else:
                del self.apps[app["uniqueName"]]
                self.deleted.append(app["uniqueName"])
            return 204, None

        if match := re.fullmatch(r"/directory/deletedItems/(.+)", path):
            return 204, None

        if match := re.fullmatch(r"/servicePrincipals\(appId='([^']+)'\)", path):
            return (200, self.sps[match.group(1)]) if match.group(1) in self.sps else (404, None)

        if path == "/servicePrincipals":
            sp = {"id": f"sp-{body['appId']}", "appId": body["appId"]}
            self.sps[body["appId"]] = sp
            return 201, sp

        if path.startswith("/oauth2PermissionGrants"):
            if method == "GET":
                client, resource = re.findall(r"'([^']+)'", path)[:2]
                return 200, {"value": [grant for grant in self.grants if grant["clientId"] == client and grant["resourceId"] == resource]}
            grant = {"id": str(uuid.uuid4()), **body}
            self.grants.append(grant)
            return 201, grant

        if path.startswith("/identity/authenticationEventsFlows"):
            if match := re.fullmatch(r"/identity/authenticationEventsFlows/([^/]+)/conditions/applications/includeApplications", path):
                if method == "GET":
                    return 200, {"value": [{"appId": app_id} for app_id in self.flow_apps.get(match.group(1), [])]}
                self.flow_apps.setdefault(match.group(1), []).append(body["appId"])
                return 201, None
            if method == "GET":
                name = re.findall(r"'([^']+)'", path)[0]
                return 200, {"value": [flow for flow in self.flows if flow["displayName"] == name]}
            if method == "DELETE":
                self.flows = [flow for flow in self.flows if not path.endswith(flow["id"])]
                return 204, None
            flow = {"id": str(uuid.uuid4()), **body}
            self.flows.append(flow)
            return 201, flow

        if path.startswith("/organization"):
            return 200, {"value": [{"verifiedDomains": [{"name": "hospitalityosdev.onmicrosoft.com", "isInitial": True}]}]}

        if path.startswith("/users"):
            email = re.findall(r"issuerAssignedId eq '([^']+)'", path)[0]
            return 200, {"value": [user for user in self.users if user["identities"][0]["issuerAssignedId"] == email]}

        if match := re.fullmatch(r"/servicePrincipals/([^/]+)/appRoleAssignedTo(?:/(.+))?", path):
            if method == "GET":
                return 200, {"value": list(self.assigned)}
            if method == "DELETE":
                self.assigned = [entry for entry in self.assigned if entry["id"] != match.group(2)]
                return 204, None
            entry = {"id": str(uuid.uuid4()), **body}
            self.assigned.append(entry)
            return 201, entry

        raise AssertionError(f"Unexpected {method} {path}")


def target(env: str = "dev") -> Target:
    placement = Placement("signin", "azure", "uksouth", "identity", (), {"service": "External ID", "type": "ciam", "driver": "graph", "module": "azure/signin/apps.json"})
    return Target(placement, env, "hospitalityos")


class GraphDriverTests(unittest.TestCase):
    def setUp(self):
        self.fake = FakeGraph()
        self.driver = GraphDriver(TENANT, Graph(lambda: "token", self.fake.send))

    def test_deploy_registers_the_api_and_staff_apps_consents_and_links_the_user_flow(self):
        outputs = self.driver.deploy(target(), {})

        api = self.fake.apps["signin-dev-uksouth-ciam-api"]
        staff = self.fake.apps["signin-dev-uksouth-ciam-staff-web"]
        self.assertEqual([f"api://{api['appId']}"], api["identifierUris"])
        self.assertEqual(["access_as_user"], [scope["value"] for scope in api["api"]["oauth2PermissionScopes"]])
        self.assertEqual(["Operator"], [role["value"] for role in api["appRoles"]])
        self.assertEqual(["http://localhost:5174"], staff["spa"]["redirectUris"])
        self.assertEqual("HospitalityOS Staff (dev)", staff["displayName"])
        self.assertEqual(
            {(f"sp-{staff['appId']}", f"sp-{api['appId']}", "access_as_user"), (f"sp-{staff['appId']}", "sp-graph", "openid offline_access")},
            {(grant["clientId"], grant["resourceId"], grant["scope"]) for grant in self.fake.grants if grant["clientId"] == f"sp-{staff['appId']}"},
        )
        self.assertEqual([staff["appId"], self.fake.apps["signin-dev-uksouth-ciam-admin-web"]["appId"]], self.fake.flow_apps[self.fake.flows[0]["id"]])
        self.assertEqual(
            {
                "tenantId": "tenant-1",
                "issuer": "https://tenant-1.ciamlogin.com/tenant-1/v2.0",
                "authority": "https://hospitalityosdev.ciamlogin.com/tenant-1/v2.0",
                "spaAuthority": "https://hospitalityosdev.ciamlogin.com/tenant-1",
                "apiClientId": api["appId"],
                "scope": f"api://{api['appId']}/access_as_user",
                "staffWebClientId": staff["appId"],
                "adminWebClientId": self.fake.apps["signin-dev-uksouth-ciam-admin-web"]["appId"],
            },
            outputs,
        )

    def test_deploying_again_creates_nothing_new_and_finds_no_drift(self):
        first = self.driver.deploy(target(), {})
        counts = (len(self.fake.apps), len(self.fake.sps), len(self.fake.grants), len(self.fake.flows), sum(len(apps) for apps in self.fake.flow_apps.values()))

        second = self.driver.deploy(target(), {})

        self.assertEqual(first, second)
        self.assertEqual(counts, (len(self.fake.apps), len(self.fake.sps), len(self.fake.grants), len(self.fake.flows), sum(len(apps) for apps in self.fake.flow_apps.values())))
        self.assertEqual([], self.driver.changes(target(), {}))
        self.assertEqual(first, self.driver.outputs(target()))

    def test_drift_names_what_differs(self):
        self.driver.deploy(target(), {})
        self.fake.apps["signin-dev-uksouth-ciam-staff-web"]["spa"]["redirectUris"] = ["https://elsewhere.example"]
        self.fake.apps["signin-dev-uksouth-ciam-api"]["appRoles"] = []
        self.fake.flow_apps.clear()

        self.assertEqual(
            [
                "app signin-dev-uksouth-ciam-api: app roles differ",
                "app signin-dev-uksouth-ciam-staff-web: redirect URIs differ",
                "app signin-dev-uksouth-ciam-staff-web: not on the user flow",
                "app signin-dev-uksouth-ciam-admin-web: not on the user flow",
            ],
            self.driver.changes(target(), {}),
        )

    def test_not_deployed_has_no_outputs_and_is_drift(self):
        self.assertIsNone(self.driver.outputs(target()))
        self.assertEqual(["app signin-dev-uksouth-ciam-api is missing"], self.driver.changes(target(), {}))

    def test_remove_deletes_the_flow_and_every_app_for_good(self):
        self.driver.deploy(target(), {})

        self.driver.remove(target())
        self.driver.remove(target())

        self.assertEqual({}, self.fake.apps)
        self.assertEqual([], self.fake.flows)
        self.assertEqual(["signin-dev-uksouth-ciam-staff-web", "signin-dev-uksouth-ciam-admin-web", "signin-dev-uksouth-ciam-api"], self.fake.deleted)
        self.assertEqual(3, sum(1 for method, path in self.fake.writes if path == "/directory/deletedItems/<id>"))

    def test_operators_are_granted_and_revoked_by_the_email_they_signed_up_with(self):
        self.driver.deploy(target(), {})
        graph = self.driver.graph

        self.assertEqual("Ops Person", grant_operator(graph, "signin-dev-uksouth-ciam-api", "ops@example.com"))
        grant_operator(graph, "signin-dev-uksouth-ciam-api", "ops@example.com")
        self.assertEqual([("user-ops", self.fake.apps["signin-dev-uksouth-ciam-api"]["appRoles"][0]["id"])], [(entry["principalId"], entry["appRoleId"]) for entry in self.fake.assigned])

        revoke_operator(graph, "signin-dev-uksouth-ciam-api", "ops@example.com")
        self.assertEqual([], self.fake.assigned)

        with self.assertRaises(PlanError) as nobody:
            grant_operator(graph, "signin-dev-uksouth-ciam-api", "stranger@example.com")
        self.assertIn("Nobody has signed up with stranger@example.com", str(nobody.exception))


class SignInTenantTests(unittest.TestCase):
    def test_the_tenant_comes_from_the_environment_and_every_part_is_needed(self):
        environ = {"HOSPITALITYOS_SIGNIN_TENANT_ID": "t", "HOSPITALITYOS_SIGNIN_DOMAIN": "d", "HOSPITALITYOS_SIGNIN_CLIENT_ID": "c"}

        self.assertEqual(SignInTenant("t", "d", "c"), SignInTenant.from_environment(environ))
        with self.assertRaises(PlanError) as missing:
            SignInTenant.from_environment({**environ, "HOSPITALITYOS_SIGNIN_CLIENT_ID": ""})
        self.assertIn("HOSPITALITYOS_SIGNIN_CLIENT_ID", str(missing.exception))

    def test_outside_workflows_the_token_comes_from_the_azure_cli_for_that_tenant(self):
        import hospitalityos_deploy.graph as module

        asked = []
        original = module.az_graph_token
        module.az_graph_token = lambda tenant_id: asked.append(tenant_id) or "cli-token"
        try:
            self.assertEqual("cli-token", graph_token(TENANT, environ={}))
        finally:
            module.az_graph_token = original
        self.assertEqual(["tenant-1"], asked)


class GraphPlacementTests(unittest.TestCase):
    def test_a_service_naming_the_graph_driver_is_deployed_by_it_not_by_its_cloud(self):
        from tests.test_deployer import FakeCloud

        azure, graph = FakeCloud(), FakeCloud({"signin": {"apiClientId": "api-1"}})
        signin = target().placement
        plan = Plan("azure-only", "", [Placement("logs", "azure", "uksouth", "platform", (), {"service": "log", "type": "log"}), signin])

        Deployer(plan, "dev", "hospitalityos", {"azure": azure, "graph": graph}, log=lambda _: None).up(sha=None, smoke=False)
        Deployer(plan, "dev", "hospitalityos", {"azure": azure, "graph": graph}, log=lambda _: None).down()

        self.assertEqual(["logs"], [call[1] for call in azure.calls if call[0] == "deploy"])
        self.assertEqual(["signin"], [call[1] for call in graph.calls if call[0] == "deploy"])
        self.assertIn(("finish", ("signin",)), graph.calls)
        self.assertIn(("finish", ("logs",)), azure.calls)
        self.assertEqual(["azure", "graph"], plan.drivers())
        self.assertTrue((DEPLOY / signin.service["module"]).exists())


class GitHubVariableTests(unittest.TestCase):
    def test_each_environment_has_its_own_repository_variables(self):
        from hospitalityos_deploy.graph import github_variables

        self.assertEqual(
            {
                "HOSPITALITYOS_SIGNIN_TENANT_ID": "SIGNIN_TENANT_ID_DEV",
                "HOSPITALITYOS_SIGNIN_DOMAIN": "SIGNIN_DOMAIN_DEV",
                "HOSPITALITYOS_SIGNIN_CLIENT_ID": "SIGNIN_CLIENT_ID_DEV",
            },
            github_variables("dev"),
        )
