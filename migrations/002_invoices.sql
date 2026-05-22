CREATE SEQUENCE invoice_number_seq;

CREATE TABLE invoices (
    id                  UUID PRIMARY KEY,
    order_id            UUID NOT NULL UNIQUE REFERENCES orders(id) ON DELETE CASCADE,
    invoice_number      BIGINT NOT NULL UNIQUE DEFAULT nextval('invoice_number_seq'),
    issued_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    contact_email       TEXT NOT NULL,
    buyer_name          TEXT NULL,
    buyer_address_line1 TEXT NULL,
    buyer_address_line2 TEXT NULL,
    buyer_city          TEXT NULL,
    buyer_region        TEXT NULL,
    buyer_postal_code   TEXT NULL,
    buyer_country       TEXT NULL
);
CREATE INDEX ix_invoices_order_id ON invoices (order_id);

CREATE TABLE invoice_line_items (
    id           UUID PRIMARY KEY,
    invoice_id   UUID NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
    product_id   UUID NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    licence_id   UUID NOT NULL REFERENCES licences(id) ON DELETE RESTRICT,
    product_name TEXT NOT NULL,
    product_slug TEXT NOT NULL,
    label        TEXT NULL,
    unit_price   NUMERIC(12, 2) NULL,
    currency     CHAR(3) NOT NULL
);
CREATE INDEX ix_invoice_line_items_invoice_id ON invoice_line_items (invoice_id);

INSERT INTO invoices (id, order_id, issued_at, contact_email)
SELECT gen_random_uuid(), o.id, o.created_at, o.contact_email
FROM orders o
ORDER BY o.created_at, o.id;

INSERT INTO invoice_line_items (id, invoice_id, product_id, licence_id, product_name, product_slug, label, unit_price, currency)
SELECT gen_random_uuid(), inv.id, oi.product_id, oi.licence_id, p.display_name, p.slug, l.label, oi.unit_price, oi.currency
FROM order_items oi
JOIN invoices inv ON inv.order_id = oi.order_id
JOIN products p ON p.id = oi.product_id
JOIN licences l ON l.id = oi.licence_id;
