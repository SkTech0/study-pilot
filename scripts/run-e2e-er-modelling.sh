#!/usr/bin/env bash
# Run E2E test using ER Modelling.pdf. Requires API and AI service to be running.
# Usage: from repo root (with API on 5024 and Python AI on 8000):
#   ./scripts/run-e2e-er-modelling.sh

set -e
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PDF="$REPO_ROOT/ER Modelling.pdf"
if [[ ! -f "$PDF" ]]; then
  echo "Error: ER Modelling.pdf not found at $PDF"
  exit 1
fi
export E2E_PDF="ER Modelling.pdf"
exec "$REPO_ROOT/scripts/e2e-mock-test.sh"
