# Security release gates

Do not deploy GhoghnoosAcc as a multi-customer public SaaS until all items below pass.

- [ ] ASP.NET Core Identity is configured; no plaintext passwords.
- [ ] Login, logout, lockout, recovery and secure cookie settings are tested.
- [ ] Anonymous requests to all accounting APIs are denied.
- [x] Accounting API policy fails closed unless the authenticated principal has a valid tenant claim.
- [x] Tenant claim is issued only after validating an active server-side membership; multiple memberships require explicit validated selection.
- [x] Tenant ownership columns and tenant-scoped unique indexes are represented in the model and a forward migration is added.
- [ ] Migration has been applied and tested against a copy of the current PostgreSQL database; do not apply it to the only copy of production data.
- [ ] Migration row counts and accounting totals are verified before and after application.
- [ ] All implemented accounting queries use a shared tenant filter; endpoints for parties and remaining accounting operations still need to be implemented and reviewed.
- [ ] Cross-tenant foreign-key references are rejected by application validation and database constraints.
- [x] Role policies are enforced for current endpoints: Owner/Admin/Accountant may write; known roles may read. Unit tests cover role authorization.
- [ ] Fine-grained Sales/Service permissions and endpoint-level integration tests remain to be completed.
- [ ] Staff cannot grant themselves owner/platform privileges.
- [ ] Create/edit/delete/post/export permissions are enforced by server endpoints.
- [ ] Integration tests prove tenant A cannot read or mutate tenant B data (current unit tests verify shared tenant-query filtering, but are not full HTTP/database integration tests).
- [ ] Posting sales/service invoices is transactional and idempotent.
- [ ] Secrets are not committed to source control.
- [ ] HTTPS, rate limiting, audit logs, health checks and backups are configured.
- [ ] Backup restoration has been tested.
- [ ] CI build and automated tests pass.

## Migration behavior and limitations

The tenant-ownership migration creates the tenant and membership tables if needed, creates one fixed-ID **Legacy Workspace**, and assigns all existing accounting rows to that workspace so existing rows are not left without an owner. It does **not** automatically grant any user membership or owner privileges. The migration is intentionally irreversible because dropping tenant IDs could combine customer data. Review and test it against a backup/copy first.

The current login flow issues tenant claims only after validating active server-side membership and provides a server-validated tenant selection page for users with multiple active memberships. The accounting API requires a valid tenant and recognized role; reads allow known membership roles, while writes are limited to Owner/Admin/Accountant. Implemented account and journal-entry queries use a shared tenant filter, and composite tenant-aware foreign keys are configured in the EF model. This is still not production-ready: the migration has not been applied, no end-to-end tenant isolation tests have been run, role provisioning and fine-grained Sales/Service permissions remain incomplete, and all endpoints/data paths must be reviewed before deployment.
