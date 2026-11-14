# 0012. The API connects with a data-only database role

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

The API used the database owner (in Docker the container's superuser) for everything. A SQL injection or a bug in
the API could then drop tables, change the schema or read other databases on the server. The API only ever needs to
read and write rows; changing the schema is the job of migrations ([ADR 0002](0002-schema-and-dbcontext-per-module.md)).

## Decision

- **Two roles.** The owner (`POSTGRES_USER`) owns every schema and runs the migrations. The API's role
  (`POSTGRES_APP_USER`, default `kamada_app`) may `SELECT`, `INSERT`, `UPDATE` and `DELETE` and use the identity
  sequences – nothing else: no `CREATE`, `DROP`, `ALTER` or `TRUNCATE`, no superuser, no new roles or databases.
- **Granted once, for the future.** `deploy/postgres/app-role.sql` creates the role and sets *default privileges* of the
  owner (`ALTER DEFAULT PRIVILEGES FOR ROLE owner ...`), so the schemas and tables every later migration creates are
  usable by the app without a grant per migration. In Docker it runs from `docker-entrypoint-initdb.d` when the volume
  is initialised; elsewhere a superuser runs it once with `psql -v` variables before the first migration.
- **Two connection strings.** `ConnectionStrings:Kamada` is the app role. The optional
  `ConnectionStrings:KamadaMigrations` is the owner: when set, the API applies migrations on start with it (in a scope of
  its own) and uses the app role for everything else. Without it (local `dotnet run`) one connection does both, as before.

## Consequences

- An existing database needs the role once (the script, then grants on tables that already exist:
  `GRANT ... ON ALL TABLES IN SCHEMA ...`); in Docker the simplest way is a new volume.
- The owner's password must still reach the API container while migrations run on start. The production setup
  (stage 5) moves migrations to a separate step, and then the API container no longer holds the owner's password.
- An integration test runs the script on a fresh database, starts the API with both connection strings and checks that
  the app role can sign a worker in but cannot create, drop, alter or truncate anything.
