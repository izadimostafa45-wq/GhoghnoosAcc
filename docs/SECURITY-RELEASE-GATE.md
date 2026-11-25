# Security release gates

Do not deploy GhoghnoosAcc as a multi-customer public SaaS until all items below pass.

- [ ] ASP.NET Core Identity is configured; no plaintext passwords.
- [ ] Login, logout, lockout, recovery and secure cookie settings are tested.
- [ ] Anonymous requests to all accounting APIs are denied.
- [x] Accounting API policy fails closed unless the authenticated principal has a valid tenant claim.
- [ ] Tenant claim is issued only after validating an active server-side membership.
- [x] Tenant ownership columns and tenant-scoped unique indexes are represented in the model and a forward migration is added.
- [ ] Migration has been applied and tested against a copy of the current PostgreSQL database; do not apply it to the only copy of production data.
- [ ] Migration row counts and accounting totals are verified before and after application.
- [ ] All accounting reads and writes are filtered by the authenticated tenant, including accounts, parties, entries and entry lines.
- [ ] Cross-tenant foreign-key references are rejected by application validation and database constraints.
- [ ] Owner, admin, accountant, sales, service and read-only policies are tested.
- [ ] Staff cannot grant themselves owner/platform privileges.
- [ ] Create/edit/delete/post/export permissions are enforced by server endpoints.
- [ ] Integration tests prove tenant A cannot read or mutate tenant B data.
- [ ] Posting sales/service invoices is transactional and idempotent.
- [ ] Secrets are not committed to source control.
- [ ] HTTPS, rate limiting, audit logs, health checks and backups are configured.
- [ ] Backup restoration has been tested.
- [ ] CI build and automated tests pass.

## Migration behavior and limitations

The tenant-ownership migration creates the tenant and membership tables if needed, creates one fixed-ID **Legacy Workspace**, and assigns all existing accounting rows to that workspace so existing rows are not left without an owner. It does **not** automatically grant any user membership or owner privileges. The migration is intentionally irreversible because dropping tenant IDs could combine customer data. Review and test it against a backup/copy first.

The /api/accounting group remains fail-closed: no trusted tenant claim is issued by the current login flow, so these endpoints stay unavailable. This branch adds ownership fields and tenant-scoped uniqueness, but it does not yet implement membership provisioning, tenant-scoped endpoint queries, cross-tenant relationship validation, or full integration tests. Do not merge or deploy as multi-tenant SaaS until those blockers are completed.
