# GhoghnoosAcc — SaaS implementation plan

## Current baseline
- ASP.NET Core Razor Pages, .NET 10, EF Core/Npgsql and PostgreSQL.
- Existing accounting data models: Party, Account, JournalEntry, JournalEntryLine.
- Existing accounting API endpoints currently operate on one shared data set and do not require authentication.
- Database startup uses EnsureCreatedAsync; it does not evolve an existing schema.
- Do not expose the current application publicly to multiple customers.

## Non-negotiable security invariants
1. Every business-owned record must have a tenant ownership path.
2. Tenant identity comes from authenticated server-side membership, never solely from a client-supplied TenantId.
3. Every read, create, update, delete, export and report is tenant-scoped on the server.
4. Cross-tenant foreign keys are rejected.
5. Authorization is enforced on endpoints/handlers, not merely by hiding navigation links.
6. Passwords are handled only by ASP.NET Core Identity; never store plaintext passwords.
7. Tenant-scoped uniqueness is used for account codes and document numbers.
8. Security-sensitive events are auditable.

## Target domain
- Tenant: business workspace, name, status, created time and subscription reference.
- ApplicationUser: login identity managed by ASP.NET Core Identity.
- TenantMembership: UserId, TenantId, role, active status and staff profile.
- Roles: Owner, Admin, Accountant, Sales, Service, ReadOnly.
- Granular permissions: view/create/edit/delete/post/export, evaluated server-side.
- SubscriptionPlan and TenantSubscription: plan limits and lifecycle.
- AuditEvent: actor, tenant, action, entity, timestamp and correlation identifier.

## Safe implementation sequence
### Phase 1 — persistence migration
- Add EF Core Design and tooling dependencies.
- Introduce a reviewed migration workflow; stop relying on EnsureCreatedAsync for schema evolution.
- Back up the existing local database before applying changes.
- Create a first tenant and backfill existing accounts, parties, journal entries and lines to it.
- Preserve current records; verify counts and debit/credit totals before and after migration.
- Add tenant-scoped unique indexes only after checking duplicate data.

### Phase 2 — identity and workspace onboarding
- Add ASP.NET Core Identity with secure cookies, lockout, password reset and anti-forgery protections.
- Add registration/sign-in/sign-out and first-owner workspace creation.
- Add tenant membership and active-workspace selection.
- Keep platform operator access separate from tenant owner access.

### Phase 3 — enforce isolation
- Add tenant scoping to all accounting endpoints and page handlers.
- Reject cross-tenant account/party/document references.
- Protect every write with authorization policies and anti-forgery controls where applicable.
- Add integration tests for cross-tenant read/write attempts and unauthenticated requests.
- Do not merge or deploy until these tests pass.

### Phase 4 — staff and permissions
- Owner/admin can invite or create staff within their own tenant.
- Enforce least-privilege policies server-side.
- Add account status, revocation and audit trails.
- Do not allow tenant owners to grant platform-admin privileges.

### Phase 5 — sales and services
- Add product/service catalog, sales invoices and lines, customer/supplier relationships, receipts/payments and inventory movements where needed.
- Posting an invoice and creating its balanced journal entry must occur in one database transaction.
- Use idempotency safeguards to avoid duplicate posting.
- Configure taxes by supported jurisdiction; do not hard-code an assumed tax regime.

### Phase 6 — subscriptions and operations
- Plans and limits for businesses/users/features.
- Subscription lifecycle, billing integration and suspension policy.
- HTTPS, secrets management, rate limiting, structured logs, health checks and backup/restore drills.
- Validate tenant isolation and restore from backup before public launch.

## Definition of done
- Existing local accounting records survive migration.
- Anonymous users cannot access accounting data or mutation endpoints.
- Two test tenants cannot read or mutate each other's data.
- Every staff permission is enforced server-side and has automated coverage.
- Sales/service posting creates exactly one balanced journal entry transactionally.
- CI build and automated tests pass.
- A documented rollback and backup/restore procedure exists.
