#!/usr/bin/env bash
# Run StudyPilot stack locally (no Docker) on macOS/Linux:
# - PostgreSQL must be running with DB "StudyPilot" and user studypilot/password postgres
# - AI service (Python) on http://localhost:8000
# - .NET API on http://localhost:5024
# - Frontend (Angular) on http://localhost:4200
#
# Prereqs: Python 3.12+, pip install -e . in study-pilot-ai; .NET SDK; Node for frontend.
# Usage: from repo root: ./scripts/run-local.sh
# Press Ctrl+C to stop all services.

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# PIDs to kill on exit
PIDS=()

cleanup() {
  echo ""
  echo "Stopping services..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  exit 0
}
trap cleanup SIGINT SIGTERM

echo "StudyPilot - run locally (no Docker)"
echo "Repo root: $REPO_ROOT"
echo ""

# Optional: check PostgreSQL
if command -v nc &>/dev/null; then
  if nc -z localhost 5432 2>/dev/null; then
    echo "[OK] PostgreSQL reachable at localhost:5432"
  else
    echo "[WARN] PostgreSQL not reachable at localhost:5432. Ensure Postgres is running and DB exists."
    echo "  Example: psql -U postgres -f scripts/postgres-setup.sql"
  fi
fi
echo ""

# 1) AI service (Python) - port 8000
AI_DIR="$REPO_ROOT/study-pilot-ai"
if [[ -d "$AI_DIR/.venv" ]]; then
  PYTHON="$AI_DIR/.venv/bin/python"
else
  PYTHON="$(command -v python3 2>/dev/null || command -v python 2>/dev/null)"
fi
if [[ -z "$PYTHON" || ! -x "$PYTHON" ]]; then
  echo "[ERROR] Python not found. Create a venv: cd study-pilot-ai && python3 -m venv .venv && .venv/bin/pip install -e ."
  exit 1
fi
if [[ ! -f "$AI_DIR/.env" ]]; then
  echo "[WARN] study-pilot-ai/.env not found. Copy from .env.example and set GEMINI_API_KEY (or OPENAI_API_KEY)."
fi
echo "Starting AI service (Python) at http://localhost:8000 ..."
(
  cd "$AI_DIR"
  export AI_MODE="${AI_MODE:-mock}"
  "$PYTHON" -m pip install -e . -q 2>/dev/null || true
  exec "$PYTHON" -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
) &
PIDS+=($!)
sleep 2

# 2) .NET API - port 5024 (Development so AIService:BaseUrl = http://localhost:8000)
echo "Starting .NET API at http://localhost:5024 ..."
(
  cd "$REPO_ROOT"
  export ASPNETCORE_ENVIRONMENT=Development
  dotnet run --project src/StudyPilot.API/StudyPilot.API.csproj
) &
PIDS+=($!)
sleep 2

# 3) Frontend - port 4200
APP_DIR="$REPO_ROOT/study-pilot-app"
if [[ -f "$APP_DIR/package.json" ]]; then
  echo "Starting frontend at http://localhost:4200 ..."
  (
    cd "$APP_DIR"
    npm install --silent 2>/dev/null || true
    npm run start
  ) &
  PIDS+=($!)
else
  echo "Frontend (study-pilot-app) not found; skipping."
fi

echo ""
echo "All services started. Press Ctrl+C to stop all."
echo "  API:       http://localhost:5024"
echo "  AI:        http://localhost:8000"
echo "  Frontend:  http://localhost:4200  (proxies /api to 5024)"
echo "  Health:    http://localhost:5024/health/ready"
echo ""

wait
