ALTER TABLE audit_events DROP CONSTRAINT IF EXISTS audit_events_subject_type_check;
ALTER TABLE audit_events
    ADD CONSTRAINT audit_events_subject_type_check
        CHECK (subject_type IN ('user', 'licence', 'order', 'product'));

CREATE TABLE product_files (
    id                    UUID PRIMARY KEY,
    product_id            UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    version_number        INTEGER NOT NULL,
    file_name             TEXT NOT NULL,
    storage_path          TEXT NOT NULL,
    content_type          TEXT NOT NULL,
    file_size_bytes       BIGINT NOT NULL,
    uploaded_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    uploaded_by_admin_id  UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT product_files_version_unique UNIQUE (product_id, version_number)
);
CREATE INDEX ix_product_files_product_latest ON product_files (product_id, version_number DESC);
