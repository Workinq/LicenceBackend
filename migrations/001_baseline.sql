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

CREATE TABLE audit_events (
    id            UUID PRIMARY KEY,
    occurred_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    event_type    TEXT NOT NULL,
    subject_type  TEXT NOT NULL CHECK (subject_type IN ('user', 'licence')),
    subject_id    UUID NOT NULL,
    actor_type    TEXT NOT NULL CHECK (actor_type IN ('admin', 'system', 'anonymous')),
    actor_user_id UUID NULL REFERENCES users(id) ON DELETE RESTRICT,
    reason        TEXT NULL,
    payload       JSONB NOT NULL,
    CONSTRAINT audit_events_actor_user_id_when_admin
        CHECK ((actor_type = 'admin' AND actor_user_id IS NOT NULL)
            OR (actor_type IN ('system', 'anonymous') AND actor_user_id IS NULL))
);
CREATE INDEX ix_audit_events_subject
    ON audit_events (subject_type, subject_id, occurred_at DESC);
CREATE INDEX ix_audit_events_event_type
    ON audit_events (event_type, occurred_at DESC);
CREATE INDEX ix_audit_events_denied_verifies
    ON audit_events (occurred_at DESC)
    WHERE event_type = 'licence.verified' AND (payload->>'outcome') = 'denied';
