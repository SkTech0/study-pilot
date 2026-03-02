# Running StudyPilot Locally

## Prerequisites

- **.NET 10** (or .NET 9) SDK  
- **Node.js 18+** and npm  
- **PostgreSQL 16** (running locally, or use Docker only for Postgres)  
- **Python 3.12** (for AI service)  
- **OpenAI API key** (for AI features)

---

## Option A: Full stack with Docker (easiest)

From the repo root:

```bash
cd deploy
cp .env .env.local
# Edit .env.local: set OPENAI_API_KEY=sk-... and optionally Jwt__Secret, ConnectionStrings__Default
docker compose up --build
```Request URL
http://localhost:4200/api/progress/weak-topics
Request Method
GET
Status Code
500 Internal Server Error
Remote Address
[::1]:4200
Referrer Policy
strict-origin-when-cross-origin

- **Frontend:** http://localhost:8080  
- **API:** http://localhost:8081  
- **AI service:** internal  
- **Postgres:** internal  

---

## Option B: Run everything on the host (no Docker)

### 1. PostgreSQL

Create DB and user:

```sql
CREATE DATABASE "StudyPilot";
CREATE USER studypilot WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE "StudyPilot" TO studypilot;
```

Or use defaults: database `StudyPilot`, user `postgres`, password `postgres` (already in `appsettings.Development.json`).

### 2. AI service (Python)

```bash
cd study-pilot-ai
pip install -e .
export OPENAI_API_KEY=sk-your-key
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
```

### 3. Backend API (.NET)

```bash
cd src/StudyPilot.API
dotnet run
```

Uses `appsettings.Development.json` (DB: localhost, AI: http://localhost:8000).  
API: http://localhost:5000 (HTTP) or https://localhost:5001 (HTTPS).

### 4. Frontend (Angular)

```bash
cd study-pilot-app
npm install
npm start
```

Runs at http://localhost:4200 and proxies `/api` to http://localhost:5000.

---

## Health & version

- **Liveness:** GET http://localhost:5000/health/live  
- **Readiness:** GET http://localhost:5000/health/ready  
- **Version:** GET http://localhost:5000/version  

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
