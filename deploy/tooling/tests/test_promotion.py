"""The promotion path's guarantees, as the catalog and promote.yml state them."""

import re
import unittest

from hospitalityos_deploy.model import DEPLOY, load_catalog

PROMOTE = DEPLOY.parent / ".github" / "workflows" / "promote.yml"


def job(name: str) -> str:
    """One job's block of promote.yml: from its name to the next job."""
    text = PROMOTE.read_text(encoding="utf-8")
    match = re.search(rf"(?ms)^  {name}:\n(.*?)(?=^  [a-z-]+:\n|\Z)", text)
    assert match, f"promote.yml has no job {name}"
    return match.group(1)


class PromotionTests(unittest.TestCase):
    def test_prod_is_never_seeded(self):
        seed = load_catalog()["resources"]["migrate"]["services"]["azure"]["parameters"]["seed"]

        self.assertFalse(seed["prod"])

    def test_prod_takes_tests_image_only_after_the_live_evals_on_test_pass(self):
        prod = job("prod")

        self.assertRegex(prod, r"needs: \[[^\]]*\bevals\b[^\]]*\]")
        self.assertIn("environment: prod", prod)
        self.assertIn("promote-from: test", prod)

    def test_the_live_evals_run_on_test_after_it_deploys_from_devs_image(self):
        evals, test = job("evals"), job("test")

        self.assertRegex(evals, r"needs: \[[^\]]*\btest\b[^\]]*\]")
        self.assertIn("environment: test", evals)
        self.assertIn("uses: ./.github/workflows/ai-evals-live.yml", evals)
        self.assertIn("promote-from: dev", test)


class WorkflowTests(unittest.TestCase):
    def test_drift_is_checked_in_every_environment_the_catalog_names_passing_those_not_yet_deployed(self):
        text = (DEPLOY.parent / ".github" / "workflows" / "drift-detection.yml").read_text(encoding="utf-8")
        listed = re.search(r"env: \[([^\]]*)\]", text)

        self.assertIsNotNone(listed)
        self.assertEqual(load_catalog()["environments"], [env.strip() for env in listed.group(1).split(",")])
        self.assertIn("--skip-undeployed", text)

    def test_dev_can_be_redeployed_at_an_earlier_commit_to_roll_back(self):
        text = (DEPLOY.parent / ".github" / "workflows" / "deploy-dev.yml").read_text(encoding="utf-8")

        self.assertIn("sha: ${{ inputs.sha ||", text)


if __name__ == "__main__":
    unittest.main()
