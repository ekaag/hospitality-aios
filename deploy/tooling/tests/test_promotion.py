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


if __name__ == "__main__":
    unittest.main()
