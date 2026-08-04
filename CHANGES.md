# Changes

## In Development

## v1.6.3 (2026-08-04)

- fix: self-hosted instances no longer require an active Desk seat for live API keys. The seat guard in `AppPageModel` and the profile save check were both keyed on `!IsStandalone`, so every multi-user deployment — including self-hosted ones with `DESK_HOSTED` unset — was treated as the hosted deployment and sent to the `NoSeat` page. Both now check `IsHosted`, matching the documented pricing (self-hosted Desk is free, no seat required). The `NoSeat` page picks its copy and its "Go to Dashboard" button on the same flag. `DeskConfig.IsHosted` is now a settable property (still defaulting to the `DESK_HOSTED` environment variable) so the deployment mode can be exercised in tests.
- chore: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` bumped to `10.0.7` to align with the ASP.NET Core DataProtection patch level (CVE-2026-40372, advisory dotnet/announcements#395). Desk was not exploitable — the vulnerable `Microsoft.AspNetCore.DataProtection` binary is served by the shared framework via .NET 10 package pruning — but the transitive version is now on the fixed patch level. The test projects' `System.Security.Cryptography.Xml` pin was lifted from `10.0.6` to `10.0.7` to satisfy the new transitive floor (still covers GHSA-37gx-xxp4-5rgx/CVE-2026-33116 and GHSA-w3x6-4m5h-cxqf/CVE-2026-26171).

## v1.6.2 (2026-04-21)

- perf: the Sent invoices grid now reads the SDI state from the new `Send.latest_state` inline field (API v1.12), collapsing the former 1+N update requests into a single `/send` call per page. Requires API ≥ 1.12.
- chore: `MailKit` bumped to `4.16.0` to neutralise GHSA-9j88-vvj5-vhgr (STARTTLS response injection / SASL downgrade, medium severity, affects `< 4.16.0`). `System.Security.Cryptography.Xml` pinned to `10.0.6` in the test projects to neutralise GHSA-37gx-xxp4-5rgx/CVE-2026-33116 and GHSA-w3x6-4m5h-cxqf/CVE-2026-26171 (both DoS, high severity, affect `< 10.0.6`); the main project prunes the package via .NET 10 framework package pruning, so no pin is needed there. `EmailService` gates SMTP authentication on both username and password being non-empty (correctness improvement surfaced by MailKit's tighter nullable annotations).

## v1.6.1 (2026-04-17)

- new: `ApiClient` now recognises HTTP 429 from the API and surfaces a dedicated "too many requests" message, honouring the `Retry-After` header (delta-seconds or HTTP-date) when present. Other non-2xx responses keep the existing generic handling.

## v1.6.0 (2026-04-12)

- new: sandbox API keys (containing `_test_`) no longer require an active Desk seat in hosted mode. Only live keys (`_live_`) are gated by the seat check, both at runtime (`AppPageModel`) and at profile save time. This lowers the barrier to evaluate Desk: users can fully test the integration with a sandbox key and only subscribe when switching to live.
- new: profile API key hint, welcome banner, "no active seat" error and `NoSeat` page now explicitly mention that sandbox (test) keys always work without a seat — only live (production) keys require an active Desk seat.
- chore: removed all references to the "15-day free trial" from the profile/`NoSeat` copy. With sandbox keys now free and unrestricted, the trial is logically redundant.
- fix: `.desk-alert` no longer uses `display: flex`, which was inserting visible `gap` whitespace before and after inline elements (e.g. `<a>` links inside the "no active seat" warning). Alerts now use plain block layout — no alert in the codebase had an icon, so the flex container had no purpose.

## v1.5.2 (2026-04-09)

- fix: post-registration redirect now lands on the profile page with the welcome banner, so new users immediately see that an API key with an active Desk seat is required to start using Desk.
- fix: localize the "API key is required" inline error in the profile page (was a hard-coded English string).
- new: log API key save rejections (empty input, invalid/unreachable, no active seat) at Information level with the user id/email so onboarding drop-offs can be diagnosed from the logs.
- new: daily rolling log files under `logs/` in production, with 30-day retention and a one-line HTTP request summary per request. Configurable via a new `logging:` section in `desk.yml`. Both `docker-compose.yml` and `docker-compose.standalone.yml` now bind-mount `./logs:/app/logs` by default so logs are visible from the host with `tail -f`. Note: create the `./logs` directory on the host before the first `docker compose up`, otherwise compose will refuse to create the container.
- chore: removed orphaned billing files left over from the v1.5.0 billing removal (`BillingProfileModel.cs`, `Countries.cs`, and the four `Subscription*.html` mail templates). No functional change — these files were no longer referenced anywhere in the codebase.

## v1.5.1 (2026-04-08)

- new: User-Agent header now reports the deployment mode (`standalone`, `self-hosted`, `hosted`) so the API can distinguish official hosted Desk traffic from self-hosted instances. The hosted deployment opts in via the `DESK_HOSTED=true` environment variable.

## v1.5.0 (2026-04-07)

- breaking: rimosso billing/Stripe da Desk; l'accesso cloud è ora gestito tramite seat API/Dashboard.
- new: controllo seat via API; redirect a pagina di errore se la chiave non ha un seat attivo.
- new: nascosti contatori operazioni/firme per utenti con chiave secondaria.
- chore: semplificata registrazione e profilo (rimossi campi fiscali e billing).
- chore: rimosse risorse .resx orfane relative a billing e profilo fiscale.
- chore: nessun impatto su installazioni self-hosted (standalone o multi-utente).
- fix: copy del profilo e della pagina NoSeat ora chiarisce che serve una API key con postazione Desk attiva; in modalità hosted la pagina NoSeat propone direttamente "Vai alla Dashboard".
- fix: salvataggio API key nel profilo ora rifiuta una key valida ma senza postazione Desk (in modalità hosted), con messaggio dedicato e link alla sezione Chiavi della Dashboard.
- fix: link alla Dashboard puntano direttamente alla sezione Chiavi (`/SubKeys`) per accorciare il flusso di abilitazione Desk.

## v1.4.2 (2026-04-02)

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
