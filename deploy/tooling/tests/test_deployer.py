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

    def ensure_image(self, target, registry, repository, tag, project) -> None:
        self.calls.append(("image", repository, tag, registry["loginServer"], project))


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


class TeardownGuardTests(unittest.TestCase):
    def test_tearing_down_needs_the_environment_named_again(self):
        self.assertEqual(1, main(["down", "--env", "dev", "--plan", "azure-only"]))
        self.assertEqual(1, main(["down", "--env", "dev", "--plan", "azure-only", "--confirm", "prod"]))


if __name__ == "__main__":
    unittest.main()
