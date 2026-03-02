-- Grant schema public permissions (PostgreSQL 15+). Run when database and user already exist.
-- Usage: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql

GRANT USAGE ON SCHEMA public TO studypilot;
GRANT CREATE ON SCHEMA public TO studypilot;
