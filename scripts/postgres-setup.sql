-- Run as PostgreSQL superuser (e.g. postgres).
-- Usage: psql -U postgres -f scripts/postgres-setup.sql
--
-- If the database or user already exist, skip the CREATE lines and run from \c onward.

CREATE DATABASE "StudyPilot";
CREATE USER studypilot WITH PASSWORD 'postgres';
GRANT ALL PRIVILEGES ON DATABASE "StudyPilot" TO studypilot;

-- Connect to the app database and grant schema permissions (required on PostgreSQL 15+).
\c "StudyPilot"

GRANT USAGE ON SCHEMA public TO studypilot;
GRANT CREATE ON SCHEMA public TO studypilot;
