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

### Quick start

**macOS / Linux** (from repo root):

```bash
./scripts/run-local.sh
```

This starts the AI service (port 8000), .NET API (port 5024), and frontend (port 4200) in the background. Press **Ctrl+C** to stop all services.

**Windows PowerShell** (from repo root):

```powershell
.\scripts\run-local.ps1
```

This opens three windows: AI service (port 8000), .NET API (port 5024), and frontend (port 4200).

**Before running**, ensure PostgreSQL is running and the database exists (see below).

### 1. PostgreSQL
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = 'StudyPilot'
  AND pid <> pg_backend_pid();

  DROP DATABASE "StudyPilot";
Install and start PostgreSQL, then create the DB and user. **PostgreSQL 15+** no longer grants create on `public` by default, so grant schema permissions too:

```sql
CREATE DATABASE "StudyPilot";
CREATE USER studypilot WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE "StudyPilot" TO studypilot;
CREATE EXTENSION IF NOT EXISTS vector;
-- Required on PostgreSQL 15+: allow app user to run migrations (create tables in public)
\c "StudyPilot"
GRANT USAGE ON SCHEMA public TO studypilot;
GRANT CREATE ON SCHEMA public TO studypilot;
GRANT ALL ON ALL TABLES IN SCHEMA public TO studypilot;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO studypilot;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO studypilot;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO studypilot;
```

Run the first block as a superuser (e.g. `psql -U postgres`). The `\c "StudyPilot"` connects to the database; then run the rest.

**Or use the repo scripts (from repo root):**

- First-time setup (create DB + user + grants):  
  `psql -U postgres -f scripts/postgres-setup.sql`
- Database and user already exist (grants + pgvector extension):  
  `psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql`  
  This script creates the `vector` extension (required for AI knowledge retrieval) and grants on `public`. Must be run as superuser (`postgres`).

Connection is set in `appsettings.Development.json`: `Host=localhost;Database=StudyPilot;Username=studypilot;Password=postgres`.

**Schema (single migration):** The database is brought up to date with one migration. From repo root run once:  
`dotnet ef database update --project src/StudyPilot.Infrastructure --startup-project src/StudyPilot.API`  
This applies the `InitialCreate` migration (all tables, indexes, and the `vector` extension).

### 2. AI service (Python)

The Python service uses a **provider adapter**: it tries providers in the order of `LLM_FALLBACK_CHAIN`; only providers that have an API key are used. If you set only `GEMINI_API_KEY`, you get Gemini only. To use multiple providers (or a different primary), set the keys and chain in `study-pilot-ai/.env`:

- **Chain (order = primary → fallback):** `LLM_FALLBACK_CHAIN=gemini,deepseek,openrouter,openai`. On 429 or failure the next provider is tried quickly so users don’t notice.
- **Keys:** Set one or more of `GEMINI_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`, `OPENAI_API_KEY` (see `study-pilot-ai/.env.example`).

At startup the service logs which chain is active, e.g. `LLM provider chain (adapter): gemini` or `LLM provider chain (adapter): openai, gemini`.

**First-time setup (use a virtual environment — required on macOS with Homebrew Python):**

```bash
cd study-pilot-ai
python3 -m venv .venv
source .venv/bin/activate
pip install -e .
```

**Run the AI service:**

```bash
cd study-pilot-ai
source .venv/bin/activate   # if not already activated
# Ensure .env exists (copy from .env.example) and set GEMINI_API_KEY=your-key
python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

Or in one line (no activate): `./.venv/bin/python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000`

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

### Document stays "Processing" or everything feels slow

- **Document status "Processing"**  
  After upload, a background job calls the **Python AI service** (port 8000) to extract concepts. The document stays "Processing" until that finishes.  
  - Ensure the **AI service is running**: `uvicorn app.main:app --reload --host 0.0.0.0 --port 8000` in `study-pilot-ai`.  
  - First run can take **30 seconds–2 minutes** depending on document size and API (e.g. Gemini).  
  - If the AI service is down or slow, the job will eventually fail and the document will show "Failed" with a reason.

- **Quiz first question takes a long time**  
  Questions are now generated **on demand** (lazy). The first time you open a question, the backend calls the AI to generate it, so the first load can take **5–30+ seconds**. Later questions may load faster if prefetched. This is expected. In Development, retry delays between failed attempts are shorter (see `AIService:QuestionGenerationRetryBaseDelayMs` in `appsettings.Development.json`).

### 503 `AI_SERVICE_UNAVAILABLE` on `/api/tutor/respond`, `/api/chat/*`, quiz generation

The .NET API calls the **Python AI service** at `AIService:BaseUrl`. If that request fails (service not running or wrong URL), you get 503.

**Fix:**

1. **Start the Python AI service** (must be running before using tutor, chat, or quiz):
   ```bash
   cd study-pilot-ai && ./.venv/bin/python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
   ```
2. **Use the correct BaseUrl:** For local run, the API uses `appsettings.Development.json`, which sets `AIService:BaseUrl` to `http://localhost:8000`. If you run the API without the Development environment, set `AIService__BaseUrl=http://localhost:8000` or add it to your appsettings.
3. **API keys:** For real (non-mock) AI, set at least one key in `study-pilot-ai/.env` (e.g. `GEMINI_API_KEY`). See `config/ai.env.example` for the full list.

### `ECONNREFUSED` on `/documents`, `/health/ai`, `/progress/weak-topics`

The frontend proxies `/api` to **http://localhost:5024**. If the .NET API is not running, you get connection refused.

**Fix:** Start the backend API (see step 3 above), or run everything with:

```powershell
.\scripts\run-local.ps1
```

That starts the AI service (8000), .NET API (5024), and frontend (4200) in separate windows.

### Gemini `429 Too Many Requests`

When the primary provider (e.g. Gemini) returns 429, the app does one short retry (~2s) then **fails over to the next provider** in `LLM_FALLBACK_CHAIN` (e.g. DeepSeek, OpenRouter, OpenAI) so users see minimal delay.

- **To get seamless switching:** Set multiple API keys in `study-pilot-ai/.env` (e.g. `GEMINI_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`). Only providers with keys set are used, in the order of `LLM_FALLBACK_CHAIN`.
- **If you only have Gemini:** You’ll get one quick retry then an error; wait a few minutes or use a key with higher quota.

- **OpenRouter 404:** If OpenRouter returns 404, the model ID may be invalid or deprecated. Set `OPENROUTER_MODEL` in `study-pilot-ai/.env` to a current model (e.g. `google/gemini-2.5-flash` or for free tier `google/gemini-2.0-flash-exp:free`). See [openrouter.ai/models](https://openrouter.ai/models).

---

## Health & version

- **Liveness:** GET http://localhost:5024/health/live  
- **Readiness:** GET http://localhost:5024/health/ready  
- **Version:** GET http://localhost:5024/version  

---

## Central AI configuration (API keys + service URL)

All AI-related settings are documented in one place:

- **Reference file:** `config/ai.env.example`  
  Lists the .NET API service URL (`AIService:BaseUrl`) and every LLM API key used by the Python service.

- **.NET API** (talks to Python AI):  
  - Local: `src/StudyPilot.API/appsettings.Development.json` → `AIService:BaseUrl: "http://localhost:8000"`  
  - Override with env: `AIService__BaseUrl=http://localhost:8000`

- **Python AI** (LLM + embeddings):  
  - Copy `study-pilot-ai/.env.example` to `study-pilot-ai/.env` and set at least one of:  
    `GEMINI_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`, `OPENAI_API_KEY`  
  - For local dev without real LLM calls use `AI_MODE=mock` in `study-pilot-ai/.env`.

- **Docker:** Set keys and `AIService__BaseUrl` in `deploy/.env` (see `deploy/.env.example`).

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
