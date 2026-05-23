ALTER TABLE licences DROP CONSTRAINT IF EXISTS licences_key_pepper_version_when_keyed;
DROP INDEX IF EXISTS ix_licences_key_hmac;
ALTER TABLE licences DROP COLUMN IF EXISTS key_hmac;
ALTER TABLE licences DROP COLUMN IF EXISTS key_hmac_pepper_version;
