# Changes

## In Development

- fix: post-registration redirect now lands on the profile page with the welcome banner, so new users immediately see that an API key with an active Desk seat is required to start using Desk.
- fix: localize the "API key is required" inline error in the profile page (was a hard-coded English string).
- new: log API key save rejections (empty input, invalid/unreachable, no active seat) at Information level with the user id/email so onboarding drop-offs can be diagnosed from the logs.
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
