-- Grant schema public permissions (PostgreSQL 15+). Run when database and user already exist.
-- Requires superuser (postgres). Creates pgvector extension for AI knowledge retrieval.
-- Usage: psql -U postgres -d StudyPilot -f scripts/postgres-grant-public.sql

CREATE EXTENSION IF NOT EXISTS vector;

GRANT USAGE ON SCHEMA public TO studypilot;
GRANT CREATE ON SCHEMA public TO studypilot;
