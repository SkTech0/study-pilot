# Running StudyPilot Locally

## Prerequisites

- **.NET 10** (or .NET 9) SDK  
- **Node.js 18+** and npm  
- **PostgreSQL 16** (running locally, or use Docker only for Postgres)  
- **Python 3.12** (for AI service)  
- **Gemini API key** (recommended) or **OpenAI API key** (for AI features)

---

## Option A: Full stack with Docker (easiest)

From the repo root:

```bash
cd deploy
cp .env.example .env
# Edit .env: set GEMINI_API_KEY=your-key (or OPENAI_API_KEY) and Jwt__Secret, ConnectionStrings__Default
docker compose up --build
```

- **Frontend:** http://localhost:8080  
- **API:** http://localhost:8081  
- **AI service:** internal  
- **Postgres:** internal  

---

## Option B: Run everything on the host (no Docker)

### Quick start (Windows PowerShell)

From the repo root, run:

```powershell
.\scripts\run-local.ps1
```

This opens three windows: AI service (port 8000), .NET API (port 5024), and frontend (port 4200).  
**Before that**, ensure PostgreSQL is running and the database exists (see below).

### 1. PostgreSQL

Install and start PostgreSQL, then create the DB and user:

```sql
CREATE DATABASE "StudyPilot";
CREATE USER studypilot WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE "StudyPilot" TO studypilot;
```

Connection is set in `appsettings.Development.json`: `Host=localhost;Database=StudyPilot;Username=studypilot;Password=postgres`.

### 2. AI service (Python)

Uses **Gemini** by default if `GEMINI_API_KEY` is set in `study-pilot-ai/.env`; otherwise set `OPENAI_API_KEY` and `LLM_PROVIDER=openai`.

```bash
cd study-pilot-ai
pip install -e .
# Ensure .env exists (copy from .env.example) and set GEMINI_API_KEY=your-key
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

Or use the `run-local.ps1` script to start it in a separate window.

### 3. Backend API (.NET)

```bash
cd src/StudyPilot.API
dotnet run
```

Uses `appsettings.Development.json` (DB: localhost, AI: http://localhost:8000).  
API: http://localhost:5024 (from launchSettings).

### 4. Frontend (Angular)

```bash
cd study-pilot-app
npm install
npm start
```

Runs at http://localhost:4200 and proxies `/api` to http://localhost:5024.

---

## Troubleshooting

### `ECONNREFUSED` on `/documents`, `/health/ai`, `/progress/weak-topics`

The frontend proxies `/api` to **http://localhost:5024**. If the .NET API is not running, you get connection refused.

**Fix:** Start the backend API (see step 3 above), or run everything with:

```powershell
.\scripts\run-local.ps1
```

That starts the AI service (8000), .NET API (5024), and frontend (4200) in separate windows.

### Gemini `429 Too Many Requests`

The AI service uses Google’s Gemini API. On the free tier, rate limits can trigger 429. The app retries with backoff (15s, 30s, 60s, …); the request may eventually succeed.

- **If it keeps failing:** Wait a few minutes and try again, or use a Gemini API key with higher quota.
- **To reduce 429s:** Avoid starting many documents or quizzes in quick succession.

---

## Health & version

- **Liveness:** GET http://localhost:5024/health/live  
- **Readiness:** GET http://localhost:5024/health/ready  
- **Version:** GET http://localhost:5024/version  

---

## If the API fails at startup

Startup requires:

- `Jwt:Secret` or `Jwt:Key` (min 32 chars)  
- `ConnectionStrings:Default` or `ConnectionStrings:DefaultConnection`  
- `AIService:BaseUrl`  
- `AIService:TimeoutSeconds`  

For local runs these are set in `src/StudyPilot.API/appsettings.Development.json`.  
For Docker, set them in `deploy/.env` (see Option A).

---

## E2E test with curl

From the repo root, run the script that tests **upload document** and **start quiz** against the .NET API and optionally the Python AI:

```bash
# Ensure API (e.g. http://localhost:5024) and Python AI (http://localhost:8000) are running.
export TEST_PDF_PATH="/path/to/a/small.pdf"   # optional; if set, upload is tested
./scripts/test-upload-and-quiz.sh
```

The script will: register/login, optionally upload a PDF, start quiz for that document, and call the Python AI endpoints directly (extract-concepts, generate-quiz). Start quiz will only succeed once the document has been processed (concepts extracted via the background job that calls the Python API).
