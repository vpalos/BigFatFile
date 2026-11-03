-- Sign-in picks the tenant that the user signed in to last.
ALTER TABLE tenant_members ADD COLUMN last_signed_in_at timestamptz;
