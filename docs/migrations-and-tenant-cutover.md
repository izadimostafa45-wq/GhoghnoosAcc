# EF Core migration and tenant cutover runbook

## Current state

- `AppDbContext` includes ASP.NET Core Identity and the tenant/membership entities.
- The existing local PostgreSQL database was originally created with `EnsureCreatedAsync` and has no `__EFMigrationsHistory` table.
- Startup no longer calls `EnsureCreatedAsync`. Schema changes must be explicit EF Core migrations.
- This branch does **not** claim that the legacy database has been migrated. Do not expose the application as a multi-tenant service until authentication, tenant-scoped data access, authorization, and isolation tests are complete.

## Required migration workflow

1. Keep the verified custom-format backup of `GhoghnoosAccDb` outside the repository. Never commit connection strings, passwords, or backup dumps.
2. Restore that backup into a separate temporary PostgreSQL database. Do not test a migration against the only copy of the source database.
3. Review the current entity model and choose a tenant backfill strategy before generating the migration. Existing `Accounts`, `Parties`, `JournalEntries`, and `JournalEntryLines` are currently not tenant-scoped; a schema-only migration is not sufficient for tenant isolation.
4. From the project directory, generate a migration on this branch:
   ```powershell
   dotnet ef migrations add InitialIdentityAndTenantSchema
   ```
5. Inspect the generated migration and model snapshot. Confirm Identity tables, tenant and membership tables, indexes, foreign keys, and any tenant columns/backfill are intentional. Do not blindly accept destructive `DropTable`, `DropColumn`, or data-replacement operations.
6. Apply the migration only to the temporary restored database first:
   ```powershell
   dotnet ef database update
   ```
   Ensure the connection string points at the temporary database before running this command.
7. Verify row counts and accounting totals before and after migration; check debit/credit totals and foreign-key integrity. Test a fresh empty database separately.
8. Add automated tests proving anonymous access is denied and that users from one tenant cannot read or mutate another tenant's accounts, parties, entries, or lines.
9. Only after review and successful tests, schedule the migration for the intended local database. Keep the original backup until the new application version is verified.

## Release gate

Do not deploy publicly while accounting endpoints are unauthenticated or queries/writes are not constrained by a validated active tenant membership. A tenant claim must be issued server-side only after authentication and membership validation; never trust a tenant ID supplied by a request body, query string, or header.
