# Changes

## In Development

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
