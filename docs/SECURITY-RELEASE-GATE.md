# Security release gates

Do not deploy GhoghnoosAcc as a multi-customer public SaaS until all items below pass.

- [ ] ASP.NET Core Identity is configured; no plaintext passwords.
- [ ] Login, logout, lockout, recovery and secure cookie settings are tested.
- [ ] Anonymous requests to all accounting APIs are denied.
- [ ] Tenant is derived from authenticated membership on the server.
- [ ] All business-owned rows are tenant-scoped, including accounts, parties, entries and entry lines.
- [ ] Cross-tenant foreign-key references are rejected.
- [ ] Account code and document number uniqueness is tenant-scoped.
- [ ] Owner, admin, accountant, sales, service and read-only policies are tested.
- [ ] Staff cannot grant themselves owner/platform privileges.
- [ ] Create/edit/delete/post/export permissions are enforced by server endpoints.
- [ ] Database migration tested against a copy of the current database.
- [ ] Before/after row counts and accounting totals match after migration.
- [ ] Integration tests prove tenant A cannot read or mutate tenant B data.
- [ ] Posting sales/service invoices is transactional and idempotent.
- [ ] Secrets are not committed to source control.
- [ ] HTTPS, rate limiting, audit logs, health checks and backups are configured.
- [ ] Backup restoration has been tested.
- [ ] CI build and automated tests pass.

Current known blockers: accounting endpoints are not authenticated and data is not tenant-isolated. The current app must remain local/private until these blockers are fixed.
