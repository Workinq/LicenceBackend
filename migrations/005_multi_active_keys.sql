-- migrations/005_multi_active_keys.sql
-- New per-key table. Many rows per licence.
CREATE TABLE IF NOT EXISTS licence_keys (
    id                          UUID PRIMARY KEY,
    licence_id                  UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    key_hmac                    BYTEA NOT NULL,
    key_hmac_pepper_version     SMALLINT NOT NULL,
    key_prefix                  TEXT NOT NULL,
    label                       TEXT NULL,
    created_by_user_id          UUID NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen_at                TIMESTAMPTZ NULL,
    revoked_at                  TIMESTAMPTZ NULL,
    revoked_by_user_id          UUID NULL REFERENCES users(id) ON DELETE RESTRICT,
    revoke_reason               TEXT NULL,
    CONSTRAINT licence_keys_revoke_audit_consistent
        CHECK ((revoked_at IS NULL AND revoked_by_user_id IS NULL AND revoke_reason IS NULL)
            OR (revoked_at IS NOT NULL))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_licence_keys_key_hmac_active
    ON licence_keys (key_hmac) WHERE revoked_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_licence_keys_licence
    ON licence_keys (licence_id, revoked_at);

-- Backfill one row per keyed licence; prefix is hmac-hex since plaintext was never stored.
INSERT INTO licence_keys (id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, label, created_by_user_id, created_at)
SELECT gen_random_uuid(),
       id,
       key_hmac,
       key_hmac_pepper_version,
       'LIC-' || substr(encode(key_hmac, 'hex'), 1, 8) || '-...',
       NULL,
       NULL,
       created_at
FROM licences
WHERE key_hmac IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM licence_keys lk WHERE lk.licence_id = licences.id
  );

-- Capture which key issued each checkout so revoke can cascade.
ALTER TABLE licence_checkouts ADD COLUMN IF NOT EXISTS issued_with_licence_key_id UUID NULL REFERENCES licence_keys(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS ix_licence_checkouts_issued_with_key
    ON licence_checkouts (issued_with_licence_key_id) WHERE issued_with_licence_key_id IS NOT NULL;

-- New close reason for cascaded revoke. Postgres needs constraint drop+add for CHECK changes.
ALTER TABLE licence_checkout_history DROP CONSTRAINT IF EXISTS licence_checkout_history_close_reason_check;
ALTER TABLE licence_checkout_history ADD CONSTRAINT licence_checkout_history_close_reason_check
    CHECK (close_reason IN ('checkin', 'expired', 'admin_revoked', 'owner_revoked', 'key_revoked'));
