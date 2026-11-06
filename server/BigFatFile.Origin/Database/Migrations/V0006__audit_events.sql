-- One partition per month. The Origin creates them ahead of time, at startup and in a
-- daily job, and drops the old ones.
CREATE TABLE audit_events (
    id              uuid        NOT NULL DEFAULT uuidv7(),
    occurred_at     timestamptz NOT NULL DEFAULT now(),
    -- Null for an event outside any tenant, such as a failed sign-in.
    tenant_id       uuid        REFERENCES tenants (id) ON DELETE CASCADE,
    action          text        NOT NULL,
    -- Not a foreign key, so the row stays when the user is deleted.
    actor_user_id   uuid,
    agent_client_id text,
    ip_address      inet,
    user_agent      text,
    details         jsonb,
    PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

CREATE INDEX audit_events_tenant_time ON audit_events (tenant_id, occurred_at DESC, id DESC);

-- Requests may add a row outside any tenant, but no policy lets them read one.
ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
CREATE POLICY in_tenant ON audit_events USING (tenant_id = app_tenant_id());
CREATE POLICY outside_tenant ON audit_events FOR INSERT WITH CHECK (tenant_id IS NULL);
