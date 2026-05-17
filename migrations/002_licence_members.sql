CREATE TABLE licence_members (
    licence_id  UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    user_id     UUID NOT NULL REFERENCES users(id)   ON DELETE RESTRICT,
    added_by    UUID NOT NULL REFERENCES users(id)   ON DELETE RESTRICT,
    added_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (licence_id, user_id)
);
CREATE INDEX ix_licence_members_user_id ON licence_members (user_id);
