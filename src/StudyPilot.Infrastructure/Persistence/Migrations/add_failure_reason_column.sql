-- Run this once if you see: column d."FailureReason" does not exist
-- (Adds the column added by migration 20260302120000_AddDocumentFailureReason)
ALTER TABLE "Documents" ADD COLUMN IF NOT EXISTS "FailureReason" character varying(500) NULL;

-- If using EF migrations, also record that this migration was applied (optional, only if MigrateAsync never ran):
-- INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260302120000_AddDocumentFailureReason', '9.0.0') ON CONFLICT DO NOTHING;
