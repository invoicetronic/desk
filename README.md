# Invoicetronic Desk

Open-source, white-label web app for Italian electronic invoicing. Deploy with Docker, customize with your brand, manage invoices in minutes.

Desk is a ready-to-use frontend for the [Invoicetronic API](https://invoicetronic.com). ISVs and developers can self-host it, apply their own branding, and give their customers a complete invoicing interface — without writing a single line of UI code.

![Invoicetronic Desk](assets/screenshot.png)

## Features

- **Send & receive invoices** — full-text search, date filters, server-side pagination, XML download
- **Invoice detail** — metadata and complete SDI status timeline
- **Upload** — drag-and-drop multi-file upload
- **Export** — filter by month/quarter/date range, download as ZIP
- **Company management** — CRUD for companies linked to your API key
- **Dashboard** — recent invoices overview and counters
- **Two auth modes** — multi-user (Identity + login) or standalone (single API key, no login)
- **White-label** — custom app name, footer, CSS variables, logo
- **Localization** — Italian (default) and English
- **Docker ready** — multi-stage build, health check endpoint

## Quick start

### 1. Get an API key

Sign up at [invoicetronic.com](https://invoicetronic.com) and get your API key from the dashboard. Desk works with both **sandbox** and **live** API keys — the environment is determined by the key you use. Start with a sandbox key for testing, then switch to live when you're ready. See the [Sandbox](https://invoicetronic.com/en/docs/sandbox/) and [API Keys](https://invoicetronic.com/en/docs/apikeys/) documentation pages for details.

### 2. Deploy with Docker

**Standalone mode** (single API key, no login — ideal for internal networks):

```yaml
# docker-compose.yml
services:
  desk:
    image: invoicetronic/desk
    ports:
      - "8080:8080"
    volumes:
      - ./desk.yml:/app/desk.yml
```

```yaml
# desk.yml
app:
  api_key: itk_live_xxxxxxxxxx
```

```bash
docker compose up -d
```

Open `http://localhost:8080` — no registration needed, the app is ready to use.

**Multi-user mode** (each user registers and enters their own API key):

```yaml
# docker-compose.yml
services:
  desk:
    image: invoicetronic/desk
    ports:
      - "8080:8080"
    volumes:
      - ./desk.yml:/app/desk.yml    # optional
      - ./data:/app/data            # persist user database
```

```bash
docker compose up -d
```

Open `http://localhost:8080`, register, and enter your API key in the profile page.

### 3. Build from source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/invoicetronic/desk.git
cd desk
dotnet run --project src
```

The app starts at `http://localhost:5100`. Edit `src/desk.yml` to configure it (the app reads the config from the `src/` directory).

> **Note:** Safari forces HTTPS on `localhost`. Use `http://127.0.0.1:5100` instead, or trust the .NET dev certificate with `dotnet dev-certs https --trust`.

## Configuration

All configuration goes in `desk.yml`. The file is optional — sensible defaults are used when it's absent.

```yaml
app:
  # API endpoint (default: https://api.invoicetronic.com/v1)
  api_url: https://api.invoicetronic.com/v1

  # API key — if set, enables standalone mode (no login required)
  # If omitted, multi-user mode is active (registration + login)
  # api_key: itk_live_xxxxxxxxxx

  # Database (ignored in standalone mode)
  database:
    provider: sqlite    # sqlite | pgsql
    # For PostgreSQL, set connection string via env var:
    # App__Database__ConnectionString=Host=...;Database=desk;...

  # Branding
  branding:
    app_name: My Invoicing App
    footer_text: "Powered by <a href=\"https://example.com\">My Company</a>"

  # Language — if omitted, auto-detected from browser
  # locale: it    # it | en
```

Environment variables override YAML values using the `App__` prefix (e.g., `App__Database__ConnectionString`).

### Standalone vs multi-user

| | Standalone | Multi-user |
|---|---|---|
| **When** | `api_key` is set in desk.yml | `api_key` is absent |
| **Auth** | None — all pages accessible | Registration + login required |
| **API key** | Shared, from config | Per-user, stored in profile |
| **Database** | In-memory (no file on disk) | SQLite file (`desk.db`) or PostgreSQL |
| **Use case** | Internal network, VPN, single tenant | SaaS, multi-tenant, public-facing |

> **Warning**: in standalone mode anyone who can reach the host has full access. Use only in trusted networks.

## Theming

Desk uses CSS custom properties for theming. Override them by mounting a `custom/` directory.

### CSS variables

Create `custom/theme.css` to override the default palette:

```css
:root {
    --brand-primary: #1A237E;
    --brand-accent: #E91E63;
    --brand-font-heading: "Poppins", sans-serif;
}
```

### Custom logo and favicon

Place your files in the `custom/` directory:

```
custom/
├── theme.css       # CSS variable overrides
├── logo.svg        # navbar logo
└── favicon.png     # browser favicon
```

### Docker mount

```yaml
volumes:
  - ./my-theme:/app/custom
```

## Localization

Desk supports **Italian** and **English**. By default the language is auto-detected from the browser's `Accept-Language` header, with Italian as the fallback.

To force a specific language for all users, set `locale` in `desk.yml`:

```yaml
app:
  locale: en    # it | en
```

All UI strings — including Identity pages (login, registration, password reset) and validation errors — are fully localized.

## Architecture

```
Desk (this project)  →  frontend for end users (invoicing operations)
Dashboard            →  developer panel (API keys, billing, logs, webhooks)
API                  →  shared backend (invoicetronic.com/v1)
```

Desk has no billing logic — it's a pure operational frontend. Authorization is entirely in the API, driven by the API key.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10.0 + Razor Pages |
| Data grid | AG Grid Community (MIT) |
| UI | Custom CSS design system (no Bootstrap) |
| Auth | ASP.NET Core Identity |
| Database | SQLite (default) / PostgreSQL |
| Config | YAML (`desk.yml`) |
| Container | Docker multi-stage |

## Health check

```
GET /health → {"status":"healthy"}
```

## License

[Apache License 2.0](LICENSE)
