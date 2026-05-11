CREATE TABLE users (
    id              UUID PRIMARY KEY,
    email           TEXT NOT NULL,
    email_lower     TEXT NOT NULL UNIQUE,
    password_hash   TEXT NOT NULL,
    display_name    TEXT NULL,
    role            TEXT NOT NULL CHECK (role IN ('user', 'admin')),
    status          TEXT NOT NULL CHECK (status IN ('active', 'suspended')) DEFAULT 'active',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_users_email_lower ON users (email_lower);
CREATE INDEX ix_users_role ON users (role);
CREATE INDEX ix_users_status ON users (status);

CREATE TABLE products (
    id                  UUID PRIMARY KEY,
    slug                TEXT NOT NULL UNIQUE,
    display_name        TEXT NOT NULL,
    description         TEXT,
    tagline             TEXT,
    is_public           BOOLEAN NOT NULL DEFAULT TRUE,
    price               NUMERIC(12,2),
    currency            CHAR(3) NOT NULL DEFAULT 'USD',
    sort_order          INTEGER NOT NULL DEFAULT 0,
    image_path          TEXT,
    image_content_type  TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE licences (
    id                          UUID PRIMARY KEY,
    product_id                  UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    user_id                     UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    key_hmac                    BYTEA NOT NULL UNIQUE,
    key_hmac_pepper_version     SMALLINT NOT NULL DEFAULT 1,
    status                      TEXT NOT NULL CHECK (status IN ('active', 'suspended', 'revoked')),
    expires_at                  TIMESTAMPTZ NULL,
    notes                       TEXT NULL,
    hwid_hmac                   BYTEA NULL,
    hwid_hmac_pepper_version    SMALLINT NULL,
    ip_allowlist                JSONB NULL, -- NULL = unrestricted; [] = bind the first verifying IP; ["cidr",...] = restricted to these
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT licences_hwid_pepper_version_when_pinned
        CHECK ((hwid_hmac IS NULL AND hwid_hmac_pepper_version IS NULL)
            OR (hwid_hmac IS NOT NULL AND hwid_hmac_pepper_version IS NOT NULL))
);
CREATE INDEX ix_licences_key_hmac ON licences (key_hmac);
CREATE INDEX ix_licences_product_id ON licences (product_id);
CREATE INDEX ix_licences_user_id ON licences (user_id);
CREATE INDEX ix_licences_status ON licences (status);
CREATE INDEX ix_licences_created_at ON licences (created_at DESC);

CREATE TABLE user_status_history (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    previous_status TEXT NOT NULL CHECK (previous_status IN ('active', 'suspended')),
    new_status      TEXT NOT NULL CHECK (new_status IN ('active', 'suspended')),
    changed_by      UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    changed_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason          TEXT NULL
);
CREATE INDEX ix_user_status_history_user_id ON user_status_history (user_id, changed_at DESC);

CREATE TABLE licence_status_history (
    id              UUID PRIMARY KEY,
    licence_id      UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    previous_status TEXT NOT NULL CHECK (previous_status IN ('active', 'suspended', 'revoked')),
    new_status      TEXT NOT NULL CHECK (new_status IN ('active', 'suspended', 'revoked')),
    changed_by      UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    changed_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason          TEXT NULL
);
CREATE INDEX ix_licence_status_history_licence_id ON licence_status_history (licence_id, changed_at DESC);

CREATE TABLE session_refresh_tokens (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    token_hash      BYTEA NOT NULL UNIQUE,
    issued_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL,
    revoked_at      TIMESTAMPTZ NULL,
    replaced_by     UUID NULL REFERENCES session_refresh_tokens(id) ON DELETE RESTRICT
);
CREATE INDEX ix_session_refresh_tokens_user_active ON session_refresh_tokens (user_id) WHERE revoked_at IS NULL;

CREATE TABLE licence_binding_history (
    id                   UUID PRIMARY KEY,
    licence_id           UUID NOT NULL REFERENCES licences(id) ON DELETE CASCADE,
    binding_type         TEXT NOT NULL CHECK (binding_type IN ('hwid', 'ip_allowlist')),
    previous_value       JSONB NULL,
    new_value            JSONB NULL,
    change_source        TEXT NOT NULL CHECK (change_source IN ('admin', 'first_use')),
    changed_by_user_id   UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    changed_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason               TEXT NULL
);
CREATE INDEX ix_licence_binding_history_licence_changed
    ON licence_binding_history (licence_id, changed_at DESC);

CREATE TABLE licence_key_history (
    id                        UUID PRIMARY KEY,
    licence_id                UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    previous_key_hmac         BYTEA NOT NULL,
    previous_key_pepper_ver   SMALLINT NOT NULL,
    new_key_hmac              BYTEA NOT NULL,
    new_key_pepper_ver        SMALLINT NOT NULL,
    changed_by                UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    changed_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason                    TEXT NULL
);
CREATE INDEX ix_licence_key_history_licence_id ON licence_key_history (licence_id, changed_at DESC);

CREATE TABLE licence_verification_attempts (
    id                   UUID PRIMARY KEY,
    licence_id           UUID NOT NULL REFERENCES licences(id) ON DELETE CASCADE,
    product_id_requested UUID NULL,
    hwid_hmac            BYTEA NULL,
    source_ip            INET NOT NULL,
    outcome              TEXT NOT NULL CHECK (outcome IN ('approved', 'denied')),
    denial_reason        TEXT NULL CHECK (denial_reason IN (
        'product_mismatch',
        'licence_not_usable',
        'owner_suspended',
        'ip_not_allowlisted',
        'hwid_missing',
        'hwid_mismatch'
    )),
    attempted_at         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_licence_verification_attempts_licence_time
    ON licence_verification_attempts (licence_id, attempted_at DESC);
CREATE INDEX ix_licence_verification_attempts_denials_time
    ON licence_verification_attempts (attempted_at DESC)
    WHERE outcome = 'denied';
