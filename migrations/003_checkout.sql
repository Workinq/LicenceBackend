ALTER TABLE audit_events DROP CONSTRAINT IF EXISTS audit_events_actor_type_check;
ALTER TABLE audit_events DROP CONSTRAINT IF EXISTS audit_events_subject_type_check;
ALTER TABLE audit_events DROP CONSTRAINT IF EXISTS audit_events_actor_user_id_when_admin;

ALTER TABLE audit_events
    ADD CONSTRAINT audit_events_actor_type_check
        CHECK (actor_type IN ('admin', 'user', 'system', 'anonymous')),
    ADD CONSTRAINT audit_events_subject_type_check
        CHECK (subject_type IN ('user', 'licence', 'order')),
    ADD CONSTRAINT audit_events_actor_user_id_required
        CHECK ((actor_type IN ('admin', 'user') AND actor_user_id IS NOT NULL)
            OR (actor_type IN ('system', 'anonymous') AND actor_user_id IS NULL));

ALTER TABLE licences ADD COLUMN label TEXT NULL;

CREATE TABLE orders (
    id            UUID PRIMARY KEY,
    user_id       UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    contact_email TEXT NOT NULL,
    status        TEXT NOT NULL CHECK (status IN ('completed', 'failed')) DEFAULT 'completed',
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_orders_user_id ON orders (user_id);
CREATE INDEX ix_orders_created_at ON orders (created_at DESC);

CREATE TABLE order_items (
    id         UUID PRIMARY KEY,
    order_id   UUID NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    licence_id UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    unit_price NUMERIC(12, 2) NULL,
    currency   CHAR(3) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_order_items_order_id ON order_items (order_id);
CREATE INDEX ix_order_items_licence_id ON order_items (licence_id);
