# ChatGPT invoice assistant integration

## Current implementation

The application exposes a tenant-scoped draft endpoint:

- `POST /api/accounting/assistant/invoice-drafts`
- Requires an authenticated application session, an active tenant selection, and the existing `TenantWrite` permission.
- Resolves the party by an exact case-insensitive name match within the current tenant only.
- Rejects missing or ambiguous party names instead of guessing.
- Validates line descriptions, quantities, prices, discounts and tax rates, then calculates totals server-side.
- Creates a **Draft** invoice only. It does not post the invoice, create a journal entry, or trigger payment.
- The endpoint never accepts a tenant ID from the request body; tenant scope comes from the authenticated server-side session.

Example request:

```json
{
  "kind": "Purchase",
  "partyName": "نام تأمین‌کننده",
  "invoiceDate": "2026-11-27",
  "number": null,
  "notes": "ثبت اولیه از دستیار",
  "lines": [
    {
      "description": "کالای نمونه",
      "quantity": 2,
      "unitPrice": 150000,
      "discount": 0,
      "taxRate": 0
    }
  ]
}
```

The response includes the created draft ID and calculated totals. The caller should show the draft summary to the user and request confirmation before any separate final-posting operation.

## Important connection boundary

This endpoint is groundwork, **not yet a direct connection from the ChatGPT conversation**. It currently uses the app's existing session/tenant authorization, which is appropriate for same-site use but is not a complete authentication flow for a remote ChatGPT Action.

Before enabling a remote action, deploy the app behind public HTTPS and implement an explicit supported integration authentication flow (prefer OAuth; otherwise use revocable, tenant-scoped credentials stored hashed and mapped server-side). Never put database credentials, an integration secret, or a tenant ID in a prompt, public repository, or client-side code. The remote integration must derive tenant scope from the authenticated integration identity, enforce tenant write permissions, rate-limit requests, and log draft creation. Do not expose invoice posting or deletion to the assistant in the first release.

## Suggested ChatGPT action

Once remote OAuth/API authentication is implemented, expose only the draft endpoint first. Keep posting/finalization as a separate operation requiring explicit user confirmation and its own permission check. Do not configure a ChatGPT Action against a localhost address; ChatGPT needs a reachable HTTPS endpoint.
