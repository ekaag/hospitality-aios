"""Runs the deployment orchestrator: python deploy/tooling/deploy.py <command> (see --help)."""

import sys
from pathlib import Path

# The orchestrator's package sits beside this script; adding it explicitly also works under `python -I`.
sys.path.insert(0, str(Path(__file__).resolve().parent))

from hospitalityos_deploy.cli import main  # noqa: E402

if __name__ == "__main__":
    sys.exit(main())
