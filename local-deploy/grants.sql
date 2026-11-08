-- Database roles of the platform database. Run as the owner (postgres) after every migration; idempotent.
--   identity_app  identity-api: read/write schema identity; read core."Projects", "ProjectRoles", "ProjectMembers" (project tokens)
--   core_app      core-api: read/write schema core
--   email_app     email-api: read/write schema email
--   gateway_ro    the gateway: read-only core."Projects", core."ProjectMembers", identity."Users"
-- No role may change the schema (migrations run as postgres).

SELECT format('CREATE ROLE %I LOGIN', r) FROM unnest(ARRAY['identity_app', 'core_app', 'email_app', 'gateway_ro']) AS r
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = r) \gexec

-- Keeps the passwords in step with the secret files.
SELECT format('ALTER ROLE %I PASSWORD %L', 'identity_app', :'identity_pw') \gexec
SELECT format('ALTER ROLE %I PASSWORD %L', 'core_app', :'core_pw') \gexec
SELECT format('ALTER ROLE %I PASSWORD %L', 'email_app', :'email_pw') \gexec
SELECT format('ALTER ROLE %I PASSWORD %L', 'gateway_ro', :'gateway_pw') \gexec

REVOKE ALL ON DATABASE platform FROM PUBLIC;
GRANT CONNECT ON DATABASE platform TO identity_app, core_app, email_app, gateway_ro;

-- public holds only the citext extension (types used by identity and core). Nobody but the owner creates objects there.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO identity_app, core_app, email_app, gateway_ro;

-- Each service: its own schema.
GRANT USAGE ON SCHEMA identity TO identity_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO identity_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO identity_app;

GRANT USAGE ON SCHEMA core TO core_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA core TO core_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA core TO core_app;

GRANT USAGE ON SCHEMA email TO email_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA email TO email_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA email TO email_app;

-- Cross-schema reads: exactly the tables each reader maps, nothing else.
REVOKE ALL ON ALL TABLES IN SCHEMA core FROM identity_app, gateway_ro;
REVOKE ALL ON ALL TABLES IN SCHEMA identity FROM gateway_ro;
GRANT USAGE ON SCHEMA core TO identity_app, gateway_ro;
GRANT USAGE ON SCHEMA identity TO gateway_ro;
GRANT SELECT ON core."Projects", core."ProjectRoles", core."ProjectMembers" TO identity_app;
GRANT SELECT ON core."Projects", core."ProjectMembers", identity."Users" TO gateway_ro;
