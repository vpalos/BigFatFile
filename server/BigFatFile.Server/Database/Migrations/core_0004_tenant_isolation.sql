-- Row-level security, the second layer of tenant isolation (Architecture.Tenancy.md,
-- section 2). Each request's transaction sets app.tenant_id and app.user_id. The owner of
-- the tables is not subject to these policies.

-- A setting that an earlier transaction on the same connection set reads back as '', not
-- NULL, so NULLIF turns it back into "not set".
CREATE FUNCTION app_tenant_id() RETURNS uuid LANGUAGE sql STABLE
    AS $$ SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid $$;

CREATE FUNCTION app_user_id() RETURNS uuid LANGUAGE sql STABLE
    AS $$ SELECT NULLIF(current_setting('app.user_id', true), '')::uuid $$;

ALTER TABLE tenant_members ADD COLUMN last_signed_in_at timestamptz;

-- A user reads their own memberships in every tenant, but changes memberships only in the
-- request's tenant.
ALTER TABLE tenant_members ENABLE ROW LEVEL SECURITY;
CREATE POLICY in_tenant ON tenant_members USING (tenant_id = app_tenant_id());
CREATE POLICY of_user ON tenant_members FOR SELECT USING (user_id = app_user_id());

ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
CREATE POLICY in_tenant ON tenants USING (id = app_tenant_id());
CREATE POLICY of_user ON tenants FOR SELECT
    USING (id IN (SELECT tenant_id FROM tenant_members WHERE user_id = app_user_id()));
