"""The one-time bootstrap run from GitHub: the bootstrap workflow and what it relies on in the orchestrator."""

import re
import unittest
from unittest import mock

from hospitalityos_deploy import graph
from hospitalityos_deploy.cli import parser
from hospitalityos_deploy.model import DEPLOY

WORKFLOW = (DEPLOY.parent / ".github" / "workflows" / "bootstrap.yml").read_text(encoding="utf-8")


class BootstrapWorkflowTests(unittest.TestCase):
    def test_it_runs_only_by_hand_and_only_once_confirmed(self):
        triggers = re.search(r"(?ms)^on:\n(.*?)^\S", WORKFLOW).group(1)
        self.assertEqual(re.findall(r"(?m)^  (\w+):", triggers), ["workflow_dispatch"])
        self.assertIn("if: inputs.confirm == 'bootstrap'", WORKFLOW)

    def test_it_signs_in_as_the_person_running_it_with_a_device_code_and_stores_no_azure_credential(self):
        self.assertIn("az login --use-device-code", WORKFLOW)
        self.assertNotIn("azure/login@", WORKFLOW)
        self.assertNotIn("id-token: write", WORKFLOW)
        self.assertEqual(set(re.findall(r"secrets\.(\w+)", WORKFLOW)), {"BOOTSTRAP_GITHUB_TOKEN"})

    def test_it_bootstraps_azure_then_each_environments_tenant_setting_the_repository_variables(self):
        azure = WORKFLOW.index("deploy.py bootstrap --cloud azure")
        signin = WORKFLOW.index("deploy.py bootstrap-signin")
        self.assertLess(azure, signin)
        self.assertIn('--github --reviewer "$GITHUB_ACTOR"', WORKFLOW)
        self.assertIn("--github --device-code", WORKFLOW)
        self.assertIn("default: dev test prod", WORKFLOW)

    def test_it_signs_out_even_when_a_step_fails(self):
        self.assertRegex(WORKFLOW, r"- name: Sign out\n\s+if: always\(\)\n\s+run: az account clear")


class SignInBootstrapTests(unittest.TestCase):
    def test_signing_in_to_the_new_tenant_uses_a_device_code_only_when_asked(self):
        self.assertIn("--use-device-code", graph.tenant_login("tenant-1", device_code=True))
        self.assertNotIn("--use-device-code", graph.tenant_login("tenant-1"))
        self.assertTrue(parser().parse_args(["bootstrap-signin", "--env", "dev", "--repo", "o/r", "--domain", "d", "--device-code"]).device_code)

    def test_the_cli_is_left_on_its_subscription_so_the_next_environment_can_follow_even_after_a_failure(self):
        for failing in (False, True):
            with self.subTest(failing=failing), \
                    mock.patch("hospitalityos_deploy.azure.az_json", return_value={"id": "subscription-1"}), \
                    mock.patch("hospitalityos_deploy.azure.az") as az, \
                    mock.patch.object(graph, "_bootstrap_signin", side_effect=RuntimeError("tenant") if failing else None, return_value={"done": "yes"}):
                if failing:
                    with self.assertRaises(RuntimeError):
                        graph.bootstrap_signin("hospitalityos", "dev", "uksouth", "d", "GB", "Europe", "o/r", device_code=True)
                else:
                    graph.bootstrap_signin("hospitalityos", "dev", "uksouth", "d", "GB", "Europe", "o/r", device_code=True)

                az.assert_called_once_with("account", "set", "--subscription", "subscription-1")


if __name__ == "__main__":
    unittest.main()
