-- Keys the server signs and encrypts its OpenID Connect tokens with. Every node reads the
-- same keys from here.
CREATE TABLE server_keys (
    id           uuid        PRIMARY KEY,
    purpose      text        NOT NULL CHECK (purpose IN ('oidc-signing', 'oidc-encryption')),
    key_material text        NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    retired_at   timestamptz
);

CREATE UNIQUE INDEX server_keys_one_active ON server_keys (purpose) WHERE retired_at IS NULL;
