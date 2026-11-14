-- The API's database account: reads and writes data, cannot change the schema.
-- Variables (psql -v): owner = the role that runs migrations and owns every schema, app_user / app_password = the
-- API's role, db = the database. Run once by a superuser before the first migration (deploy/postgres/initdb).

CREATE ROLE :"app_user" LOGIN PASSWORD :'app_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
GRANT CONNECT ON DATABASE :"db" TO :"app_user";

-- Nobody but the owner creates objects in public (PostgreSQL 15+ already defaults to this).
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- Everything the owner creates later (the module schemas, their tables and identity sequences) is usable by the app:
-- data only, no CREATE / DROP / ALTER / TRUNCATE.
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" GRANT USAGE ON SCHEMAS TO :"app_user";
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" GRANT USAGE, SELECT ON SEQUENCES TO :"app_user";
