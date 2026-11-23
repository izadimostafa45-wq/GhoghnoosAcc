# GhoghnoosAcc SaaS Architecture Review

## Current-state audit (2026-11-23)

The current application is an ASP.NET Core Razor Pages application targeting .NET 10 with PostgreSQL and EF Core. It currently has:
- A shared AppDbContext with Parties, Accounts, JournalEntries and JournalEntryLines.
- Accounting APIs in Program.cs for account and journal-entry reads/writes.
- A dashboard that reads the accounting APIs.
- No ASP.NET Core Identity setup, login/logout flow, tenant/business entity, user-to-business membership, role/permission enforcement, or tenant-level data isolation.
- Database initialization through EnsureCreatedAsync. EnsureCreated does not evolve an existing schema and must not be used as a substitute for migrations.
- Account.Code and JournalEntry.DocumentNumber are globally unique today. In a multi-business system these should normally be unique per tenant.
- The accounting APIs do not currently require authentication. This must be corrected before public hosting.

## Required target architecture

### 1. SaaS platform and tenants
- Tenant (business workspace): stable ID, display/legal name, status, created timestamp, subscription/plan reference.
- Each business's operational records belong to exactly one tenant.
- A user may be a member of one or more tenants; membership selects the active workspace.
- Platform operator access is separate from customer-tenant access and must be audited.

### 2. Identity, staff and permissions
- Use ASP.NET Core Identity for password hashing, sign-in, lockout, recovery and cookie security; never store raw passwords.
- ApplicationUser represents a login identity.
- TenantMembership connects a user to a tenant and records status, role, and staff profile fields.
- Roles/permissions are checked on the server for every page handler and API. Hiding menu items is not authorization.
- Start with Owner, Admin, Accountant, Sales, Service, ReadOnly roles, then add granular permissions for view/create/edit/delete/post/export.
- Record security-sensitive actions in an audit log.

### 3. Tenant data isolation
- Add a non-null tenant ownership path to every business-owned record (directly or through a parent).
- Resolve active tenant from the authenticated membership, never from an untrusted request body alone.
- Apply tenant filters to reads and tenant-scoped predicates to updates/deletes.
- Validate that referenced accounts, parties, products and documents all belong to the same tenant.
- Use tenant-scoped unique indexes, e.g. (TenantId, Code) and (TenantId, DocumentNumber).
- Add integration tests proving Tenant A cannot read or mutate Tenant B records.

### 4. Accounting, sales and services
- Keep double-entry journal entries as the accounting source of truth.
- Add products and services as distinct item types, sales documents and lines, payments/receipts, inventory movements where applicable, tax/discount configuration and customer/supplier balances.
- Posting a sales/service invoice should create balanced accounting entries transactionally, with idempotency safeguards.
- Avoid hard-coding tax rules until supported jurisdictions and business requirements are decided.

### 5. Database and deployment
- Replace EnsureCreatedAsync with reviewed EF Core migrations before adding identity or tenant columns to the existing database.
- Preserve the existing local test accounts and journal entries through a baseline/backfill migration; assign them to an explicit initial tenant instead of silently losing or exposing them.
- Keep connection strings, signing keys and other secrets outside source control.
- Add backups, restore tests, HTTPS, rate limiting, logging, health checks and a tested upgrade/rollback process before public launch.

## Safe implementation order

1. Establish migrations and an initial tenant while preserving current local accounting data.
2. Add Identity, login/logout, tenant creation and tenant membership.
3. Enforce tenant ownership and authorization in every accounting API/page before public deployment.
4. Add staff management and server-side permission policies.
5. Build products/services, sales, receipts/payments and transactional accounting integration.
6. Add subscription administration, plan limits, audit logs and operational safeguards.
7. Test cross-tenant isolation, authorization, migrations and backup/restore.

## Safety note

Do not deploy the current version publicly as a multi-customer SaaS. The current accounting endpoints are unauthenticated and share one global data set. A tenant field or menu-level restrictions alone would not provide adequate isolation. The next code change should be a migration-backed identity/tenant slice, tested against the existing local PostgreSQL database before adding further features.