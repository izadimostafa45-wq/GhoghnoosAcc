# Remote ChatGPT integration security design

## Status

Design only. This document does not enable remote access and does not replace an implementation or security review. The current invoice-draft endpoint uses the application's interactive Identity cookie and tenant claims; do not expose it directly as a public ChatGPT Action.

## Recommended first-release boundary

- Expose draft creation only. Do not expose invoice posting, deletion, payment, or journal-entry operations to the assistant.
- Require explicit user confirmation in the accounting application before any separate finalization action.
- Keep the tenant identity server-derived. Never accept a tenant ID from the model, prompt, request body, or query string.
- Preserve the current TenantWrite authorization and active tenant/membership checks for every draft request.

## Authentication recommendation

Prefer OAuth 2.0 Authorization Code flow supported by the intended ChatGPT integration surface. Before implementation, verify the exact OAuth requirements of that surface and the application's identity model.

If OAuth cannot be supported by the chosen integration surface, use a purpose-built integration credential only after a security review. Credentials must be revocable, tenant-scoped, stored as hashes where practical, shown only once at creation, compared in constant time, and rotated/revoked without affecting the user's normal login. Do not reuse browser session cookies or Identity passwords as API credentials.

## Required server controls

1. HTTPS only; reject plain HTTP at the edge and configure trusted forwarded headers correctly when behind a reverse proxy.
2. Authenticate the integration independently of the browser cookie.
3. Resolve user, tenant, and permitted operations from validated server-side credential records; verify the tenant is active and the user has current write permission on every request.
4. Add request-size limits, rate limiting, cancellation/timeouts, and safe error responses that do not leak internal details.
5. Add an idempotency mechanism for draft creation and enforce invoice-number uniqueness in the database, not only with a pre-insert query.
6. Record an audit event for each draft attempt and outcome without logging bearer tokens, secrets, or unnecessary invoice/customer data.
7. Keep provider API keys in a deployment secret store or environment configuration; never commit secrets or place them in browser code.
8. Test unauthorized requests, revoked credentials, expired credentials, inactive tenants, cross-tenant party lookups, missing/ambiguous parties, duplicate requests/numbers, invalid precision, over-limit values, and draft-only behavior.

## Deployment gate

Do not configure a ChatGPT Action until all of the following are true:

- The app is deployed at a stable public HTTPS origin.
- The remote authentication flow is implemented and tested end-to-end.
- The action schema points only to the intended draft endpoint.
- CI passes on the exact commit intended for deployment.
- Tenant-isolation and authorization integration tests pass.
- An operator can revoke credentials and review audit events.
- A rollback path and secret-rotation procedure are documented.

## Current blockers outside GitHub

GitHub changes alone cannot create a public HTTPS deployment, provision production secrets, or complete an OAuth authorization flow with the target ChatGPT integration. Those require a chosen hosting environment, production configuration, and a later end-to-end verification. Do not put secrets in this repository or in chat.

## Repository inspection findings (2026-11-27)

- `Program.cs` configures ASP.NET Core Identity cookie authentication and maps the invoice-draft endpoint under the tenant-protected accounting route group.
- The draft endpoint requires `TenantWrite` and resolves tenant scope from `ITenantContext`; this is a useful same-application authorization boundary, but it is not a remote ChatGPT OAuth/API authentication implementation.
- The repository contains a test project at `tests/GhoghnoosAcc.Tests/GhoghnoosAcc.Tests.csproj`, and the CI workflow restores and tests that project.
- Do not add an authentication scheme that silently falls back to anonymous access, accepts tenant IDs from requests, or treats an arbitrary static secret as a user identity.

## Implementation acceptance criteria

The remote authentication implementation is not complete until it has:
- A concrete, supported OAuth configuration for the chosen ChatGPT integration surface, including issuer/authorization endpoint, token endpoint, client registration, redirect URI, scopes, and token validation.
- A server-side mapping from the validated OAuth subject to an application user and active tenant membership. Missing or ambiguous mappings must fail closed.
- Explicit authorization for draft creation on every request; read-only or revoked membership must not be able to create drafts.
- Automated tests for missing, invalid, expired, and wrong-audience tokens; inactive tenant and revoked membership; cross-tenant party names; and draft-only behavior.
- Successful CI for the exact resulting commit.

The GitHub repository does not currently provide the public deployment origin or OAuth client registration needed to complete an end-to-end connection. Those values must be configured in the hosting provider and ChatGPT integration settings, not committed here.

