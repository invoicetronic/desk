# Changes

## In Development

- fix: pass customer TaxId as a proper Stripe Tax ID instead of metadata; clean up metadata to only keep desk_user_id.

## v1.4.1 (2026-03-30)

- new: header User-Agent sulle richieste API (`Invoicetronic-Desk/<versione>`).

## v1.4.0 (2026-03-25)

- new: show hint on login failure explaining that Desk accounts are separate from Dashboard accounts.
- new: add "switch account" logout link on subscription page.
- fix: upgrade docker/build-push-action to v7 for Node.js 24 compatibility.
- fix: show actual API error messages with status code instead of generic 'error' in grids and upload page.
- fix: flaky test due to DatabaseInitializer using build-time config instead of DI config.
- fix: SQLite "database is locked" crash at startup when DataProtection key creation conflicts with open raw connection during API key encryption.

## v1.3.0 (2026-03-11)

- new: encrypt API keys at rest using ASP.NET Core Data Protection API with `ENC:` prefix; existing plaintext keys are migrated automatically at startup.
- new: persist Data Protection keys in database (SQLite/PostgreSQL) instead of filesystem; eliminates need to backup `data/keys/` separately.
- fix: upgrade GitHub Actions to Node.js 24-compatible versions (checkout v5, setup-dotnet v5, docker actions v4).

## v1.2.1 (2026-03-11)

- fix: skip SQLite directory creation when using PostgreSQL provider.

## v1.2.0 (2026-03-10)

- new: admin email notification on new user registration.
- fix: show time (hh:mm:ss) in sent invoices last update column.
- fix: document SQLite bind mount and locale fallback in desk.yml.example.

## v1.1.0 (2026-03-10)

- fix: force Italian locale in E2E tests to avoid browser Accept-Language interference.
- new: dashboard auto-refresh every 60 seconds and manual refresh button.

## v1.0.0 (2026-03-09)

- Initial release.
