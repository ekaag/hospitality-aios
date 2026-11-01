"""The catalog, plans, names, and deployment order, without any cloud."""

import json
import tempfile
import unittest
from pathlib import Path

from hospitalityos_deploy.model import (
    DEFAULT_PLAN,
    DEPLOY,
    Placement,
    PlanError,
    app_settings,
    group_name,
    load_catalog,
    load_plan,
    ordered,
    resolve_inputs,
    resource_name,
)


def catalog() -> dict:
    return {
        "product": "hospitalityos",
        "environments": ["dev", "staging"],
        "clouds": {
            "azure": {"tool": "bicep", "status": "available"},
            "gcp": {"tool": None, "status": "undecided"},
        },
        "resources": {
            "logs": {"group": "platform", "dependsOn": [], "services": {"azure": {"service": "Log Analytics", "type": "log", "module": "azure/logs.bicep"}, "gcp": {"service": "Cloud Logging"}}},
            "secrets": {
                "group": "platform",
                "dependsOn": ["logs"],
                "stateful": True,
                "services": {"azure": {"service": "Key Vault", "type": "kv", "maxLength": 24, "module": "azure/secrets.bicep", "inputs": {"workspace": "logs.workspaceId"}}},
            },
            "data": {"group": "api", "planned": "K1", "dependsOn": [], "services": {"azure": {"service": "Azure SQL"}}},
        },
    }


class Deploy:
    """A deploy/ folder in a temporary directory, with the modules the catalog names."""

    def __init__(self, plans: dict[str, dict]):
        self.directory = tempfile.TemporaryDirectory()
        self.path = Path(self.directory.name)
        (self.path / "azure").mkdir()
        (self.path / "plans").mkdir()

        for module in ("logs", "secrets"):
            (self.path / "azure" / f"{module}.bicep").write_text("// module\n", encoding="utf-8")

        for name, plan in plans.items():
            (self.path / "plans" / f"{name}.json").write_text(json.dumps(plan), encoding="utf-8")

    def load(self, name: str, env: str = "dev"):
        return load_plan(name, env, catalog(), self.path)

    def close(self):
        self.directory.cleanup()


def plan(**resources) -> dict:
    return {"name": "test", "resources": {purpose: {"cloud": cloud, "region": "uksouth"} for purpose, cloud in resources.items()}}


class NamesTests(unittest.TestCase):
    def test_resources_are_named_purpose_env_region_type(self):
        self.assertEqual("api-dev-uksouth-ca", resource_name("api", "dev", "uksouth", "ca"))
        self.assertEqual("identity-api-dev-uksouth-id", resource_name("identity-api", "dev", "uksouth", "id"))

    def test_types_without_delimiters_run_the_segments_together(self):
        self.assertEqual("imagesdevuksouthacr", resource_name("images", "dev", "uksouth", "acr", compact=True))

    def test_names_are_lowercase_segments_only(self):
        for bad in ("Api", "api_x", "-api", "api-"):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                resource_name(bad, "dev", "uksouth", "ca")

    def test_resource_groups_carry_the_product(self):
        self.assertEqual("rg-hospitalityos-platform-dev", group_name("hospitalityos", "platform", "dev"))


class PlanTests(unittest.TestCase):
    def setUp(self):
        self.deploy = Deploy(
            {
                "good": plan(logs="azure", secrets="azure"),
                "unknown": plan(logs="azure", secrets="azure", cache="azure"),
                "undecided": plan(logs="gcp", secrets="azure"),
                "unwritten": plan(logs="azure", secrets="azure", data="azure"),
                "missing": plan(secrets="azure"),
            }
        )

    def tearDown(self):
        self.deploy.close()

    def problems(self, name: str, env: str = "dev") -> list[str]:
        with self.assertRaises(PlanError) as raised:
            self.deploy.load(name, env)

        return raised.exception.problems

    def test_a_good_plan_places_each_resource_in_dependency_order(self):
        loaded = self.deploy.load("good")

        self.assertEqual(["logs", "secrets"], [placement.purpose for placement in loaded.placements])
        self.assertEqual(["azure"], loaded.clouds())

    def test_a_resource_the_catalog_does_not_know_is_refused(self):
        self.assertIn("cache: not in the catalog.", self.problems("unknown"))

    def test_a_cloud_without_tooling_is_refused(self):
        self.assertIn("logs: gcp is undecided; it has no deployment tooling yet.", self.problems("undecided"))

    def test_a_service_without_a_module_is_refused(self):
        self.assertIn("data: azure (Azure SQL) has no module yet (planned in K1).", self.problems("unwritten"))

    def test_every_resource_the_app_needs_must_be_placed_with_what_it_depends_on(self):
        problems = self.problems("missing")

        self.assertIn("logs: the plan does not place it, and the app needs it.", problems)
        self.assertIn("secrets: needs logs, which the plan does not place.", problems)

    def test_names_must_fit_each_type(self):
        self.assertIn("secrets: its name secrets-staging-uksouth-kv is 26 characters; azure allows 24.", self.problems("good", "staging"))

    def test_only_the_catalogs_environments(self):
        self.assertIn("'qa' is not an environment the catalog names (dev, staging).", self.problems("good", "qa"))

    def test_an_unknown_plan_names_the_plans_there_are(self):
        self.assertEqual(["There is no plan 'nope'. Plans: good, missing, undecided, unknown, unwritten."], self.problems("nope"))


class RepositoryTests(unittest.TestCase):
    def test_the_default_plan_is_valid_for_every_environment(self):
        for env in load_catalog()["environments"]:
            with self.subTest(env=env):
                self.assertEqual("azure-only", load_plan(DEFAULT_PLAN, env).name)

    def test_every_environment_has_a_value_for_each_per_environment_parameter(self):
        catalog = load_catalog()

        for env in catalog["environments"]:
            for purpose, entry in catalog["resources"].items():
                for parameter, values in entry["services"].get("azure", {}).get("parameters", {}).items():
                    with self.subTest(resource=purpose, parameter=parameter, env=env):
                        self.assertIn(env, values)

    def test_secure_inputs_name_environment_variables_and_conditions_name_outputs(self):
        catalog = load_catalog()
        azure = catalog["resources"]["secrets"]["services"]["azure"]
        azure["secureInputs"] = {"anthropicApiKey": "sk-ant-not-a-variable"}
        catalog["resources"]["realtime"]["services"]["azure"]["appSettingsWhen"] = {"anthropicKeySet": {"X": "y"}}

        with self.assertRaises(PlanError) as raised:
            load_plan(DEFAULT_PLAN, "dev", catalog)

        self.assertEqual(
            [
                "secrets: secure input anthropicApiKey must name an environment variable, e.g. ANTHROPIC_API_KEY, not 'sk-ant-not-a-variable'.",
                "realtime: app settings are applied when an output is true, named purpose.output, not 'anthropicKeySet'.",
            ],
            raised.exception.problems,
        )

    def test_the_deployment_identity_may_grant_every_role_the_modules_grant(self):
        import re

        bootstrap = (DEPLOY / "azure" / "bootstrap" / "main.bicep").read_text(encoding="utf-8")
        grantable = set(re.search(r"(?m)^var grantable = '([^']*)'", bootstrap).group(1).replace(" ", "").split(","))

        for module in sorted((DEPLOY / "azure" / "modules").glob("*.bicep")):
            for role in re.findall(r"roleDefinitions', '([0-9a-f-]{36})'", module.read_text(encoding="utf-8")):
                with self.subTest(module=module.name, role=role):
                    self.assertIn(role, grantable, f"{module.name} grants {role}, which the bootstrap does not let the deployment grant.")

    def test_app_settings_use_only_outputs_their_modules_declare(self):
        import re

        from hospitalityos_deploy.graph import declared_outputs, load_definition

        catalog = load_catalog()
        modules = {
            purpose: entry["services"]["azure"]["module"]
            for purpose, entry in catalog["resources"].items()
            if "module" in entry["services"].get("azure", {})
        }
        drivers = {purpose: entry["services"].get("azure", {}).get("driver", "azure") for purpose, entry in catalog["resources"].items()}

        for purpose, entry in catalog["resources"].items():
            azure = entry["services"].get("azure", {})
            templates = dict(azure.get("appSettings", {}))
            for condition, overrides in azure.get("appSettingsWhen", {}).items():
                templates[f"when {condition}"] = "${" + condition + "}"
                templates.update({f"{key} (when {condition})": value for key, value in overrides.items()})

            for key, template in templates.items():
                for source, output in re.findall(r"\$\{([a-z0-9-]+)\.([A-Za-z0-9]+)\}", template):
                    with self.subTest(setting=key):
                        self.assertIn(source, modules, f"{purpose}: {key} reads {source}, which has no Azure module.")
                        if drivers[source] == "graph":
                            declared = output in declared_outputs(load_definition(DEPLOY / modules[source]))
                        else:
                            text = (DEPLOY / modules[source]).read_text(encoding="utf-8")
                            declared = re.search(rf"(?m)^output {output} ", text) is not None
                        self.assertTrue(declared, f"{purpose}: {key} reads {source}.{output}, which its module does not output.")


def placement(purpose: str, depends_on=(), **service) -> Placement:
    return Placement(purpose, "azure", "uksouth", "platform", tuple(depends_on), {"service": purpose, "type": "x", **service})


class OrderTests(unittest.TestCase):
    def test_dependencies_come_first(self):
        result = ordered([placement("api", ["apps", "images"]), placement("images"), placement("apps", ["logs"]), placement("logs")])

        self.assertEqual(["images", "logs", "apps", "api"], [each.purpose for each in result])

    def test_a_cycle_is_refused(self):
        with self.assertRaises(PlanError):
            ordered([placement("a", ["b"]), placement("b", ["a"])])

    def test_inputs_come_from_the_outputs_of_dependencies(self):
        api = placement("api", ["images"], inputs={"registryServer": "images.loginServer"})

        self.assertEqual({"registryServer": "x.azurecr.io"}, resolve_inputs(api, {"images": {"loginServer": "x.azurecr.io"}}))

        with self.assertRaises(PlanError):
            resolve_inputs(api, {"images": {}})

    def test_conditional_app_settings_apply_only_when_their_output_is_true(self):
        from hospitalityos_deploy.model import Plan

        chat = placement(
            "inference-chat",
            appSettings={"Inference__Tasks__concierge.answer__Provider": "azure-chat", "Inference__Tasks__concierge.answer__Model": "${inference-chat.chatDeployment}"},
            appSettingsWhen={
                "secrets.anthropicKeySet": {
                    "Inference__Tasks__concierge.answer__Provider": "anthropic",
                    "Inference__Tasks__concierge.answer__Fallback__0": "azure-chat:${inference-chat.chatDeployment}",
                }
            },
        )
        deployed = Plan("p", "", [chat])

        self.assertEqual(
            {"Inference__Tasks__concierge.answer__Provider": "azure-chat", "Inference__Tasks__concierge.answer__Model": "gpt-5-mini"},
            app_settings(deployed, {"inference-chat": {"chatDeployment": "gpt-5-mini"}, "secrets": {"anthropicKeySet": False}}),
        )
        self.assertEqual(
            {
                "Inference__Tasks__concierge.answer__Provider": "anthropic",
                "Inference__Tasks__concierge.answer__Model": "gpt-5-mini",
                "Inference__Tasks__concierge.answer__Fallback__0": "azure-chat:gpt-5-mini",
            },
            app_settings(deployed, {"inference-chat": {"chatDeployment": "gpt-5-mini"}, "secrets": {"anthropicKeySet": True}}),
        )
        self.assertEqual("azure-chat", app_settings(deployed, {"inference-chat": {"chatDeployment": "gpt-5-mini"}})["Inference__Tasks__concierge.answer__Provider"])

    def test_app_settings_are_filled_from_outputs(self):
        from hospitalityos_deploy.model import Plan

        events = placement("events", appSettings={"Bindings__EventTransport__Default__Adapter": "ServiceBus", "Events__Namespace": "${events.namespace}"})

        self.assertEqual(
            {"Bindings__EventTransport__Default__Adapter": "ServiceBus", "Events__Namespace": "events-dev.servicebus.windows.net"},
            app_settings(Plan("p", "", [events]), {"events": {"namespace": "events-dev.servicebus.windows.net"}}),
        )


if __name__ == "__main__":
    unittest.main()
