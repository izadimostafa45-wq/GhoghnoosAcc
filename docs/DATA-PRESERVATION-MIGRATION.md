# Data-preserving tenant migration runbook

> Status: planning and review artifact only. Do not run this document as SQL. The application currently uses `EnsureCreatedAsync`, and the existing PostgreSQL schema has no migration history. The actual EF migration must be generated/reviewed against a database copy before rollout.

## Preconditions

1. Stop the app and take a PostgreSQL custom-format backup.
2. Restore that backup to a separate test database.
3. Record row counts for `Accounts`, `Parties`, `JournalEntries`, and `JournalEntryLines`.
4. Record total debit and credit values and verify they balance per journal entry.
5. Confirm whether the database has any pre-existing tenant/auth tables before applying a migration.
6. Do not put credentials or connection strings in source control.

## Required migration behavior

- Create a single initial workspace named `شرکت پیش‌فرض` for all legacy rows.
- Create ASP.NET Core Identity tables with the official Identity schema and password hasher; never add a plaintext password field.
- Add non-null tenant ownership to each business-owned table: accounts, parties, journal entries, and journal entry lines.
- Backfill every existing row to the default workspace before setting tenant columns NOT NULL.
- Ensure each journal line, its journal entry, account, and optional party belong to the same tenant. Enforce this in server code and, where practical, with composite database foreign keys.
- Replace globally unique account-code and document-number indexes with tenant-scoped unique indexes only after checking for duplicates within each tenant.
- Preserve existing IDs, account references, journal-line references, dates, amounts, and posting states.
- Make the migration safe for both a fresh database and a database originally created by `EnsureCreatedAsync`; do not assume an empty `__EFMigrationsHistory` means the business tables are absent.
- Avoid startup-time automatic schema changes in production. Apply reviewed migrations as a controlled deployment step.

## Verification queries

Run before and after migration and compare the results:

```sql
SELECT 'Accounts' AS entity, COUNT(*) AS row_count FROM "Accounts"
UNION ALL SELECT 'Parties', COUNT(*) FROM "Parties"
UNION ALL SELECT 'JournalEntries', COUNT(*) FROM "JournalEntries"
UNION ALL SELECT 'JournalEntryLines', COUNT(*) FROM "JournalEntryLines";

SELECT COUNT(*) AS unbalanced_entries
FROM (
    SELECT e."Id"
    FROM "JournalEntries" e
    LEFT JOIN "JournalEntryLines" l ON l."JournalEntryId" = e."Id"
    GROUP BY e."Id"
    HAVING COALESCE(SUM(l."Debit"), 0) <> COALESCE(SUM(l."Credit"), 0)
) AS balances;
```

Counts must match exactly. Debit/credit totals must match exactly. The number of unbalanced entries must not increase. Run functional tests for creating and reading a balanced entry after migration.

## Release gate

The migration is not approved until:
- a restore test succeeds;
- migration succeeds on a restored copy of the actual database;
- all row counts, IDs, relationships and accounting totals are reconciled;
- authentication is enabled and all accounting endpoints require authorization;
- integration tests prove tenant A cannot read, edit, post, or reference tenant B's records;
- CI build and tests pass.

Do not expose the current application publicly until these gates are satisfied.
