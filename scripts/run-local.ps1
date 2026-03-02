# Run StudyPilot stack locally (no Docker):
# - PostgreSQL must be running with DB "StudyPilot" and user studypilot/password postgres
# - AI service (Python) on http://localhost:8000
# - .NET API on http://localhost:5024
# - Frontend (Angular) on http://localhost:4200
#
# Prereqs: Python 3.12+, pip install -e . in study-pilot-ai; .NET SDK; Node for frontend.
# The AI service is started with AI_MODE=mock so no API keys are needed for local testing.

$ErrorActionPreference = "Stop"
$RepoRoot = if ($PSScriptRoot) { (Resolve-Path (Join-Path $PSScriptRoot "..")).Path } else { (Get-Location).Path }

Write-Host "StudyPilot - run locally (no Docker)" -ForegroundColor Cyan
Write-Host "Repo root: $RepoRoot" -ForegroundColor Gray
Write-Host ""

# Optional: check PostgreSQL (connection string from appsettings.Development.json)
$pgHost = "localhost"
$pgPort = 5432
try {
    $tcp = New-Object System.Net.Sockets.TcpClient
    $tcp.ConnectAsync($pgHost, $pgPort).Wait(2000) | Out-Null
    if ($tcp.Connected) {
        $tcp.Close()
        Write-Host "[OK] PostgreSQL reachable at ${pgHost}:${pgPort}" -ForegroundColor Green
    }
} catch {
    Write-Host "[WARN] PostgreSQL not reachable at ${pgHost}:${pgPort}. Ensure Postgres is running and DB exists." -ForegroundColor Yellow
    Write-Host "  Example: CREATE DATABASE \"StudyPilot\"; CREATE USER studypilot WITH PASSWORD 'postgres'; GRANT ALL ON DATABASE \"StudyPilot\" TO studypilot;" -ForegroundColor Gray
}
Write-Host ""

# 1) AI service (Python) - port 8000
$aiDir = Join-Path $RepoRoot "study-pilot-ai"
$aiEnv = Join-Path $aiDir ".env"
if (-not (Test-Path $aiEnv)) {
    Write-Host "[WARN] study-pilot-ai/.env not found. Copy from .env.example and set GEMINI_API_KEY (or OPENAI_API_KEY)." -ForegroundColor Yellow
}
Write-Host "Starting AI service (Python) at http://localhost:8000 (mock mode for local testing) ..." -ForegroundColor Cyan
$aiCmd = "`$env:AI_MODE='mock'; Write-Host 'AI service - port 8000 (mock mode)' -ForegroundColor Green; pip install -e .; python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $aiCmd -WorkingDirectory $aiDir
Start-Sleep -Seconds 2

# 2) .NET API - port 5024
Write-Host "Starting .NET API at http://localhost:5024 ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "dotnet run --project src\StudyPilot.API\StudyPilot.API.csproj" -WorkingDirectory $RepoRoot
Start-Sleep -Seconds 2

# 3) Frontend (optional) - port 4200
$appDir = Join-Path $RepoRoot "study-pilot-app"
if (Test-Path (Join-Path $appDir "package.json")) {
    Write-Host "Starting frontend at http://localhost:4200 ..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Frontend - port 4200' -ForegroundColor Green; npm install; npm run start" -WorkingDirectory $appDir
} else {
    Write-Host "Frontend (study-pilot-app) not found; skipping." -ForegroundColor Gray
}

Write-Host ""
Write-Host "All services started in separate windows." -ForegroundColor Green
Write-Host "  API:       http://localhost:5024" -ForegroundColor White
Write-Host "  AI:        http://localhost:8000" -ForegroundColor White
Write-Host "  Frontend:  http://localhost:4200  (proxies /api to 5024)" -ForegroundColor White
Write-Host "  Health:    http://localhost:5024/health/ready" -ForegroundColor Gray
Write-Host ""
Write-Host "Close each window to stop that service." -ForegroundColor Gray
