ALTER TABLE licences ALTER COLUMN key_hmac DROP NOT NULL;
ALTER TABLE licences ALTER COLUMN key_hmac_pepper_version DROP NOT NULL;
ALTER TABLE licences ALTER COLUMN key_hmac_pepper_version DROP DEFAULT;

ALTER TABLE licences ADD CONSTRAINT licences_key_pepper_version_when_keyed
    CHECK ((key_hmac IS NULL AND key_hmac_pepper_version IS NULL)
        OR (key_hmac IS NOT NULL AND key_hmac_pepper_version IS NOT NULL));
