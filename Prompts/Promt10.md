You are implementing STEP 9 — Deployment + Authentication Polish
for the StudyPilot production MVP.

IMPORTANT:
Generate ONLY code and configuration files.
DO NOT generate README files.
DO NOT explain anything.
NO tutorial text.
Keep output minimal and production-focused.

====================================================
EXISTING SYSTEM (DO NOT MODIFY ARCHITECTURE)
====================================================

Backend:
- .NET 10 LTS
- Clean Architecture
- CQRS (MediatR)
- JWT authentication already exists
- Background worker + AI integration complete

Frontend:
- Angular (latest LTS)
- Standalone architecture
- Built under /study-pilot-app

AI Service:
- Python FastAPI microservice

Database:
- PostgreSQL 16+

All services must run via Docker Compose.

====================================================
STEP 9 GOAL
====================================================

System runs fully via containers:

Browser
 → Angular (nginx)
 → .NET API
 → Python AI Service
 → PostgreSQL

AND authentication becomes production-safe.

====================================================
PART 1 — DOCKERIZATION
====================================================

Create folder:

/deploy

Create:

docker-compose.yml
.env (example values only)

Services:

1) frontend
   - nginx serving Angular dist
   - depends_on api

2) api
   - ASP.NET runtime image
   - environment variables only
   - no localhost assumptions

3) ai-service
   - python:3.12-slim
   - uvicorn startup

4) postgres
   - postgres:16
   - persistent volume

All services on shared docker network.

Ports:
frontend → 8080
api → 8081
ai → internal only
db → internal only

====================================================
PART 2 — BACKEND DOCKERFILE
====================================================

Create:

src/StudyPilot.API/Dockerfile

Multi-stage build:

SDK image → publish → ASP.NET runtime image.

Environment variables:

ASPNETCORE_ENVIRONMENT=Production

No secrets in image.

====================================================
PART 3 — FRONTEND DOCKERFILE
====================================================

Inside study-pilot-app:

Dockerfile:

Stage 1:
node:lts build Angular production bundle

Stage 2:
nginx:alpine serve dist.

Add nginx config:

/nginx/default.conf

Requirements:

- SPA routing support
- proxy /api → api container

====================================================
PART 4 — AI SERVICE DOCKERFILE
====================================================

Create Dockerfile for FastAPI:

- python:3.12-slim
- install requirements
- run uvicorn main:app

No hardcoded URLs.

====================================================
PART 5 — ENVIRONMENT CONFIGURATION
====================================================

All configuration via environment variables.

Examples:

ConnectionStrings__Default
Jwt__Key
AIService__BaseUrl
AIService__TimeoutSeconds

Angular environment uses:

/api base path (relative).

====================================================
PART 6 — AUTH POLISH (CRITICAL)
====================================================

Implement Refresh Token Flow.

BACKEND:

Create entity:
RefreshToken
- Id
- UserId
- Token
- ExpiresAtUtc
- RevokedAtUtc
- CreatedAtUtc

Add repository.

Endpoints:

POST /auth/refresh
POST /auth/logout

Behavior:

Login/Register returns:
- accessToken
- refreshToken

Access token:
15 minutes lifetime.

Refresh token:
7 days.

Refresh rotates token (invalidate old one).

Logout revokes refresh token.

====================================================
PART 7 — JWT IMPROVEMENTS
====================================================

JwtTokenGenerator must:

- configurable lifetimes
- include jti claim
- include role claim
- UTC timestamps only

====================================================
PART 8 — ANGULAR AUTH UPDATE
====================================================

Update AuthService:

- store refresh token securely
- auto refresh before expiry
- interceptor retries once after 401
- logout on refresh failure

No UI redesign required.

====================================================
PART 9 — HEALTH PROBES
====================================================

Add endpoints:

GET /health/live
GET /health/ready

live:
- API running

ready:
- DB reachable
- AI health reachable

Return proper HTTP status codes.

====================================================
PART 10 — PRODUCTION SAFETY
====================================================

Ensure:

- forwarded headers enabled
- HTTPS-aware configuration
- no localhost references
- environment-variable friendly config
- logging unchanged

====================================================
OUTPUT RULE
====================================================

Generate ONLY:

- Dockerfiles
- docker-compose.yml
- nginx config
- refresh token backend code
- auth controller updates
- Angular auth updates
- health endpoints

NO README.
STOP after implementation.