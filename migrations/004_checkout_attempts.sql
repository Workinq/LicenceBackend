CREATE TABLE checkout_attempts (
    id                        UUID PRIMARY KEY,
    user_id                   UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    contact_email             TEXT NOT NULL,
    currency                  CHAR(3) NOT NULL,
    amount_total              NUMERIC(12,2) NOT NULL,
    stripe_payment_intent_id  TEXT NOT NULL UNIQUE,
    status                    TEXT NOT NULL CHECK (status IN ('pending', 'fulfilled', 'failed')) DEFAULT 'pending',
    order_id                  UUID NULL REFERENCES orders(id) ON DELETE RESTRICT,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    fulfilled_at              TIMESTAMPTZ NULL
);
CREATE INDEX ix_checkout_attempts_user_id ON checkout_attempts (user_id);

CREATE TABLE checkout_attempt_items (
    id                   UUID PRIMARY KEY,
    checkout_attempt_id  UUID NOT NULL REFERENCES checkout_attempts(id) ON DELETE CASCADE,
    product_id           UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    quantity             INT NOT NULL CHECK (quantity >= 1),
    labels               JSONB NOT NULL,
    unit_price           NUMERIC(12,2) NULL,
    currency             CHAR(3) NOT NULL
);
CREATE INDEX ix_checkout_attempt_items_attempt_id ON checkout_attempt_items (checkout_attempt_id);
