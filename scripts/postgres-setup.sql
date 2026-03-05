CREATE DATABASE "StudyPilot";

CREATE USER studypilot WITH PASSWORD 'postgres';

-- connect to the new database
\c "StudyPilot"

-- install pgvector extension for embeddings
CREATE EXTENSION IF NOT EXISTS vector;

-- allow database access
GRANT ALL PRIVILEGES ON DATABASE "StudyPilot" TO studypilot;

-- allow schema usage and object creation
GRANT USAGE ON SCHEMA public TO studypilot;
GRANT CREATE ON SCHEMA public TO studypilot;

-- allow migrations to manage tables/sequences
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO studypilot;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO studypilot;

-- ensure future tables/sequences are accessible
ALTER DEFAULT PRIVILEGES IN SCHEMA public
GRANT ALL ON TABLES TO studypilot;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
GRANT ALL ON SEQUENCES TO studypilot;

-- (recommended for EF migrations)
ALTER SCHEMA public OWNER TO studypilot;
