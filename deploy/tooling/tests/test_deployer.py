"""Deploying, checking drift, and tearing down a plan, through a fake cloud driver."""

import unittest

from hospitalityos_deploy.cli import choose_plan, main
from hospitalityos_deploy.deployer import Deployer, Target
from hospitalityos_deploy.model import Placement, Plan, PlanError


class FakeCloud:
    """Records every call; a deployed resource's outputs are what `answers` says for it, plus what it was deployed with."""

    def __init__(self, answers: dict[str, dict] | None = None):
        self.answers = answers or {}
        self.deployed: dict[str, dict] = {}
        self.calls: list[tuple] = []
        self.drifted: dict[str, list[str]] = {}
        self.failing: set[str] = set()

    def deploy(self, target: Target, parameters: dict) -> dict:
        self.calls.append(("deploy", target.placement.purpose, parameters))
        outputs = {**self.answers.get(target.placement.purpose, {}), **({"image": parameters["image"]} if "image" in parameters else {})}
        self.deployed[target.placement.purpose] = outputs
        return outputs

    def outputs(self, target: Target):
        return self.deployed.get(target.placement.purpose)

    def changes(self, target: Target, parameters: dict) -> list[str]:
        self.calls.append(("changes", target.placement.purpose, parameters))
        return self.drifted.get(target.placement.purpose, [])

    def remove(self, target: Target) -> None:
        self.calls.append(("remove", target.placement.purpose))
        self.deployed.pop(target.placement.purpose, None)

    def finish_removal(self, targets: list[Target]) -> None:
        self.calls.append(("finish", tuple(target.placement.purpose for target in targets)))

    def run(self, target: Target, outputs: dict) -> None:
        self.calls.append(("run", target.placement.purpose, outputs.get("jobId")))
        if target.placement.purpose in self.failing:
            raise PlanError([f"{target.placement.purpose}: job failed"])

    def promote_image(self, target, registry, source_registry, repository, tag) -> None:
        self.calls.append(("promote", repository, tag, source_registry, registry["loginServer"]))

    def ensure_image(self, target, registry, repository, tag, project) -> None:
        self.calls.append(("image", repository, tag, registry["loginServer"], project))

    def deployer_principal_id(self) -> str:
        self.calls.append(("whoami",))
        return "deployer-oid"

    def apply_indexes(self, target, outputs, definitions) -> None:
        self.calls.append(("indexes", target.placement.purpose, outputs.get("endpoint"), tuple(path.name for path in definitions)))

    def index_changes(self, target, outputs, definitions) -> list[str]:
        return self.drifted.get(f"{target.placement.purpose}/indexes", [])


def plan() -> Plan:
    identity = Placement("identity-api", "azure", "uksouth", "platform", (), {"service": "id", "type": "id"})
    images = Placement("images", "azure", "uksouth", "platform", ("identity-api",), {"service": "acr", "type": "acr", "compact": True, "inputs": {"pullPrincipalId": "identity-api.principalId"}})
    secrets = Placement("secrets", "azure", "uksouth", "platform", ("identity-api",), {"service": "kv", "type": "kv"}, stateful=True)
    api = Placement(
        "api",
        "azure",
        "uksouth",
        "api",
        ("images", "identity-api", "secrets"),
        {"service": "ca", "type": "ca", "inputs": {"registryServer": "images.loginServer", "identityId": "identity-api.identityId"}},
        image={"project": "src/Hosts/HospitalityOS.Api", "repository": "hospitalityos-api", "registry": "images"},
    )
    return Plan("azure-only", "", [identity, images, secrets, api])


ANSWERS = {
    "identity-api": {"principalId": "p-1", "identityId": "/id/1"},
    "images": {"loginServer": "imagesdevuksouthacr.azurecr.io", "registryName": "imagesdevuksouthacr"},
    "secrets": {"vaultUri": "https://secrets-dev-uksouth-kv.vault.azure.net/"},
    "api": {"url": "https://api-dev-uksouth-ca.example"},
}


class DeployerTests(unittest.TestCase):
    def setUp(self):
        self.cloud = FakeCloud(ANSWERS)
        self.deployer = Deployer(plan(), "dev", "hospitalityos", {"azure": self.cloud}, log=lambda _: None)

    def test_up_deploys_in_order_wiring_outputs_into_inputs_and_builds_the_image_first(self):
        self.deployer.up("abc123", smoke=False)

        steps = [call[:2] if call[0] == "deploy" else call[:3] for call in self.cloud.calls]
        self.assertEqual(
            [("deploy", "identity-api"), ("deploy", "images"), ("deploy", "secrets"), ("image", "hospitalityos-api", "abc123"), ("deploy", "api")],
            steps,
        )
        images = self.cloud.calls[1][2]
        self.assertEqual(
            {
                "name": "imagesdevuksouthacr",
                "location": "uksouth",
                "tags": {"name": "images-dev-uksouth-acr", "purpose": "images", "env": "dev", "product": "hospitalityos"},
                "pullPrincipalId": "p-1",
            },
            images,
        )
        api = self.cloud.calls[4][2]
        self.assertEqual(("api-dev-uksouth-ca", "imagesdevuksouthacr.azurecr.io", "/id/1", "hospitalityos-api:abc123"), (api["name"], api["registryServer"], api["identityId"], api["image"]))
        self.assertEqual(("image", "hospitalityos-api", "abc123", "imagesdevuksouthacr.azurecr.io", "src/Hosts/HospitalityOS.Api"), self.cloud.calls[3])

    def test_stateful_resources_are_protected_in_production_only(self):
        self.deployer.up("abc123", smoke=False)
        production = Deployer(plan(), "prod", "hospitalityos", {"azure": FakeCloud(ANSWERS)}, log=lambda _: None)
        production.up("abc123", smoke=False)

        self.assertFalse(self.cloud.calls[2][2]["protect"])
        self.assertTrue(production.drivers["azure"].calls[2][2]["protect"])

    def test_secure_inputs_come_from_the_environment_and_are_empty_when_it_has_none(self):
        secured = plan()
        secured.placements[2] = Placement("secrets", "azure", "uksouth", "platform", ("identity-api",), {"service": "kv", "type": "kv", "secureInputs": {"anthropicApiKey": "ANTHROPIC_API_KEY"}}, stateful=True)

        given = FakeCloud(ANSWERS)
        Deployer(secured, "dev", "hospitalityos", {"azure": given}, log=lambda _: None, environ={"ANTHROPIC_API_KEY": "sk-ant-test"}).up("abc123", smoke=False)
        missing = FakeCloud(ANSWERS)
        Deployer(secured, "dev", "hospitalityos", {"azure": missing}, log=lambda _: None, environ={}).up("abc123", smoke=False)

        self.assertEqual("sk-ant-test", given.calls[2][2]["anthropicApiKey"])
        self.assertEqual("", missing.calls[2][2]["anthropicApiKey"])
        self.assertNotIn("anthropicApiKey", given.calls[1][2])

    def test_a_job_marked_to_run_finishes_before_what_depends_on_it_deploys_and_its_failure_stops_the_deployment(self):
        migrating = plan()
        migrate = Placement(
            "migrate", "azure", "uksouth", "api", ("images", "identity-api", "secrets"), {"service": "caj", "type": "caj", "run": True},
            image={"project": "src/Hosts/HospitalityOS.Api", "repository": "hospitalityos-api", "registry": "images"},
        )
        api = migrating.placements[3]
        migrating.placements[3:] = [migrate, Placement(api.purpose, api.cloud, api.region, api.group, (*api.depends_on, "migrate"), api.service, image=api.image)]
        cloud = FakeCloud({**ANSWERS, "migrate": {"jobId": "/jobs/migrate-dev-uksouth-caj"}})

        Deployer(migrating, "dev", "hospitalityos", {"azure": cloud}, log=lambda _: None).up("abc123", smoke=False)

        steps = [call[:2] for call in cloud.calls if call[0] in ("deploy", "run")]
        self.assertEqual([("deploy", "migrate"), ("run", "migrate"), ("deploy", "api")], steps[-3:])
        self.assertEqual(("run", "migrate", "/jobs/migrate-dev-uksouth-caj"), next(call for call in cloud.calls if call[0] == "run"))
        self.assertEqual("hospitalityos-api:abc123", next(call for call in cloud.calls if call[:2] == ("deploy", "migrate"))[2]["image"])

        failing = FakeCloud({**ANSWERS, "migrate": {"jobId": "/jobs/migrate-dev-uksouth-caj"}})
        failing.failing.add("migrate")
        with self.assertRaises(PlanError):
            Deployer(migrating, "dev", "hospitalityos", {"azure": failing}, log=lambda _: None).up("abc123", smoke=False)
        self.assertNotIn(("deploy", "api"), [call[:2] for call in failing.calls])

        cloud.calls.clear()
        Deployer(migrating, "dev", "hospitalityos", {"azure": cloud}, log=lambda _: None).drift()
        self.assertNotIn("run", [call[0] for call in cloud.calls])

    def test_promoting_copies_the_image_the_source_environment_ran_instead_of_building_it(self):
        cloud = FakeCloud({**ANSWERS, "images": {"loginServer": "imagestestuksouthacr.azurecr.io", "registryName": "imagestestuksouthacr"}})

        Deployer(plan(), "test", "hospitalityos", {"azure": cloud}, log=lambda _: None, promote_from="dev").up("abc123", smoke=False)

        self.assertIn(("promote", "hospitalityos-api", "abc123", "imagesdevuksouthacr", "imagestestuksouthacr.azurecr.io"), cloud.calls)
        self.assertNotIn("image", [call[0] for call in cloud.calls])

    def test_deployed_settings_are_what_the_deployed_resources_imply(self):
        settings_plan = plan()
        secrets = settings_plan.placements[2]
        settings_plan.placements[2] = Placement(secrets.purpose, secrets.cloud, secrets.region, secrets.group, secrets.depends_on,
                                                {**secrets.service, "appSettings": {"Bindings__SecretStore__Default__Settings__VaultUri": "${secrets.vaultUri}"}}, stateful=True)
        deployer = Deployer(settings_plan, "dev", "hospitalityos", {"azure": self.cloud}, log=lambda _: None)

        self.assertEqual({"Bindings__SecretStore__Default__Settings__VaultUri": ""}, deployer.deployed_settings())

        deployer.up("abc123", smoke=False)
        self.assertEqual({"Bindings__SecretStore__Default__Settings__VaultUri": "https://secrets-dev-uksouth-kv.vault.azure.net/"}, deployer.deployed_settings())

    def test_deploying_an_image_needs_the_commit(self):
        with self.assertRaises(PlanError):
            self.deployer.up(None, smoke=False)

    def test_drift_checks_each_resource_against_its_deployed_image(self):
        self.deployer.up("abc123", smoke=False)
        self.cloud.calls.clear()

        self.assertEqual({}, self.deployer.drift())
        self.assertEqual("hospitalityos-api:abc123", [call for call in self.cloud.calls if call[1] == "api"][0][2]["image"])

        self.cloud.drifted["secrets"] = ["Modify: /vaults/secrets-dev-uksouth-kv"]
        self.assertEqual({"secrets": ["Modify: /vaults/secrets-dev-uksouth-kv"]}, self.deployer.drift())

    def test_a_service_with_indexes_gets_them_after_it_is_deployed_and_drift_includes_them(self):
        search = Placement(
            "search-content",
            "azure",
            "uksouth",
            "platform",
            (),
            {
                "service": "srch",
                "type": "srch",
                "inputs": {"deployerPrincipalId": "deployer.principalId"},
                "parameters": {"sku": {"dev": "free", "prod": "basic"}},
                "indexes": ["azure/search/content.json", "azure/search/content-vectors.json"],
            },
        )
        cloud = FakeCloud({"search-content": {"endpoint": "https://search-content-dev-uksouth-srch.search.windows.net"}})
        deployer = Deployer(Plan("p", "", [search]), "dev", "hospitalityos", {"azure": cloud}, log=lambda _: None)

        deployer.up(None, smoke=False)

        deployed = cloud.calls[1][2]
        self.assertEqual(("whoami",), cloud.calls[0])
        self.assertEqual(("deployer-oid", "free"), (deployed["deployerPrincipalId"], deployed["sku"]))
        self.assertEqual(
            ("indexes", "search-content", "https://search-content-dev-uksouth-srch.search.windows.net", ("content.json", "content-vectors.json")),
            cloud.calls[2],
        )

        cloud.drifted["search-content/indexes"] = ["Create: index content-vectors"]
        self.assertEqual({"search-content": ["Create: index content-vectors"]}, deployer.drift())

    def test_a_resource_not_deployed_is_drift(self):
        self.assertEqual("not deployed", self.deployer.drift()["identity-api"][0])

    def test_down_removes_in_reverse_and_keeps_stateful_resources_unless_asked(self):
        self.deployer.up("abc123", smoke=False)
        self.cloud.calls.clear()

        kept = self.deployer.down()

        self.assertEqual(["secrets"], kept)
        self.assertEqual([("remove", "api"), ("remove", "images"), ("remove", "identity-api"), ("finish", ("api", "images", "identity-api"))], self.cloud.calls)

        self.cloud.calls.clear()
        self.assertEqual([], self.deployer.down(include_stateful=True))
        self.assertIn(("remove", "secrets"), self.cloud.calls)


class PlanChoiceTests(unittest.TestCase):
    def test_the_plan_given_wins(self):
        self.assertEqual("mixed", choose_plan("mixed", interactive=True))

    def test_people_are_asked_with_the_default_offered(self):
        asked = []

        self.assertEqual("azure-only", choose_plan(None, ask=lambda prompt: asked.append(prompt) or "", interactive=True))
        self.assertEqual(["Use which plan? [azure-only] "], asked)
        self.assertEqual("aws-only", choose_plan(None, ask=lambda _: "aws-only", interactive=True))

    def test_unattended_runs_use_the_default(self):
        self.assertEqual("azure-only", choose_plan(None, interactive=False))


class GitHubEnvironmentTests(unittest.TestCase):
    def test_dev_deploys_from_any_branch_test_from_main_and_prod_from_main_with_its_reviewers_approval(self):
        from hospitalityos_deploy.cli import environment_settings

        self.assertEqual({"deployment_branch_policy": None}, environment_settings("dev", 7))
        self.assertEqual({"deployment_branch_policy": {"protected_branches": False, "custom_branch_policies": True}}, environment_settings("test", 7))
        self.assertEqual(
            {
                "deployment_branch_policy": {"protected_branches": False, "custom_branch_policies": True},
                "reviewers": [{"type": "User", "id": 7}],
                "prevent_self_review": False,
            },
            environment_settings("prod", 7),
        )

        with self.assertRaises(PlanError):
            environment_settings("prod", None)

    def test_promoting_names_another_of_the_catalogs_environments(self):
        from hospitalityos_deploy.cli import deployer

        self.assertEqual("dev", deployer("test", "azure-only", "dev").promote_from)
        for source in ("test", "staging"):
            with self.subTest(source=source), self.assertRaises(PlanError):
                deployer("test", "azure-only", source)


class TeardownGuardTests(unittest.TestCase):
    def test_tearing_down_needs_the_environment_named_again(self):
        self.assertEqual(1, main(["down", "--env", "dev", "--plan", "azure-only"]))
        self.assertEqual(1, main(["down", "--env", "dev", "--plan", "azure-only", "--confirm", "prod"]))


if __name__ == "__main__":
    unittest.main()
