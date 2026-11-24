# First administrator bootstrap

This is an opt-in bootstrap for a new local installation. It does not enable public registration,
reset passwords, or modify an existing user's credentials.

## Preconditions

- The PostgreSQL connection string is configured.
- The reviewed EF Core migrations have already been applied and Identity tables exist.
- Do not use this as a substitute for the data-preserving migration runbook.

## Configure local development credentials

From the project directory, use .NET user-secrets (the project already has a UserSecretsId):

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "your-admin-email@example.com"
dotnet user-secrets set "BootstrapAdmin:Password" "REPLACE-WITH-A-UNIQUE-STRONG-PASSWORD"
```

Replace both example values before running. Do not commit the password or put it in appsettings.json.

Alternatively, environment variables are supported:

- `BootstrapAdmin__Email`
- `BootstrapAdmin__Password`

## What happens at startup

When PostgreSQL is configured and both settings are present, the application checks whether the
Identity user table contains any users. If it is empty, ASP.NET Core Identity creates the first
user and hashes the password using its configured password hasher. If any user already exists,
the bootstrap does nothing and will not reset or overwrite an account. Invalid password policy
or email settings stop startup with validation error codes in the log.

After the first successful sign-in, remove the bootstrap password setting:

```powershell
dotnet user-secrets remove "BootstrapAdmin:Password"
```

Keep the email setting only if useful for local reference, or remove it too. For production, supply
secrets through the deployment secret manager and remove the bootstrap secret immediately after
the initial account is created.

## Safety notes

- This bootstrap creates a user account; it does not grant an application-specific administrator
  role because role-based authorization policy has not yet been defined.
- It does not apply migrations or create/alter database tables.
- Do not expose the application publicly until tenant isolation, tenant-scoped data access,
  secure initial tenant assignment, and cross-tenant authorization tests are complete.
