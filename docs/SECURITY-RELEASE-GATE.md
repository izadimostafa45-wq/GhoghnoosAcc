# Security release gates

Do not deploy GhoghnoosAcc as a multi-customer public SaaS until all items below pass.

- [ ] ASP.NET Core Identity is configured; no plaintext passwords.
- [ ] Login, logout, lockout, recovery and secure cookie settings are tested.
- [ ] Anonymous requests to all accounting APIs are denied.
- [ ] Accounting API policy fails closed unless the authenticated principal has a valid tenant claim (implemented; API remains unavailable until trusted claim issuance is added).
- [ ] Tenant claim is issued only after validating an active server-side membership.
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

Current known blockers: accounting API access now requires authentication plus a valid tenant claim, but the application does not yet issue that claim after validating membership. Therefore accounting API calls are intentionally denied for current users until tenant provisioning is implemented. Business tables also lack tenant ownership columns and tenant-scoped queries, cross-tenant reference checks and integration tests are still required. Keep the app local/private; this fail-closed gate is not a substitute for complete tenant isolation.
