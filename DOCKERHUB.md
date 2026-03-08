# Invoicetronic Desk

Open-source, white-label web app for Italian electronic invoicing (FatturaPA/SDI). Self-host with Docker or use the cloud version — manage invoices in minutes, no code required.

Desk is a ready-to-use frontend for the [Invoicetronic API](https://invoicetronic.com). ISVs and developers can self-host it, apply their own branding, and give their customers a complete invoicing interface — without writing a single line of UI code.

**Don't want to self-host?** Try [Desk Cloud](https://desk.invoicetronic.com) — no Docker, no servers, no configuration. Sign up and start invoicing immediately.

## Quick start

### Standalone mode

Single API key, no login required — ideal for internal networks and quick testing:

```yaml
# docker-compose.yml
services:
  desk:
    image: invoicetronic/desk
    ports:
      - "8080:8080"
    environment:
      - Desk__api_key=YOUR_API_KEY
```

```bash
docker compose up -d
```

Open `http://localhost:8080` — the app is ready to use, no registration needed.

### Multi-user mode

Each user registers and enters their own API key — ideal for SaaS and multi-tenant deployments:

```yaml
# docker-compose.yml
services:
  desk:
    image: invoicetronic/desk
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data    # persist user database
```

```bash
docker compose up -d
```

Open `http://localhost:8080`, register, and enter your API key in the profile page.

## Get an API key

Sign up at [dashboard.invoicetronic.com](https://dashboard.invoicetronic.com) to create your API key. Desk works with both **sandbox** and **live** keys — the environment is determined by the key you use. Start with sandbox for testing, switch to live when ready.

See the [Sandbox](https://invoicetronic.com/en/docs/sandbox/) and [API Keys](https://invoicetronic.com/en/docs/apikeys/) documentation for details.

## Features

- **Send & receive invoices** — full-text search, date filters, server-side pagination, XML download
- **Invoice detail** — metadata and complete SDI status timeline
- **Upload** — drag-and-drop multi-file upload
- **Export** — filter by month/quarter/date range, download as ZIP
- **Company management** — CRUD for companies linked to your API key
- **Dashboard** — recent invoices overview and counters
- **White-label** — custom app name, logo, colors, footer via YAML config or CSS overrides
- **Two auth modes** — multi-user (Identity + login) or standalone (single API key, no login)
- **Localization** — Italian (default) and English

## Configuration

All configuration goes in `desk.yml` (mounted as `/app/desk.yml`) or via environment variables with the `Desk__` prefix.

### Branding

```yaml
# desk.yml
desk:
  branding:
    app_name: My Invoicing App
    logo_url: https://example.com/logo.svg
    primary_color: "#1A237E"
    accent_color: "#E91E63"
```

Or via environment variables:

```yaml
environment:
  - Desk__branding__app_name=My Invoicing App
  - Desk__branding__primary_color=#1A237E
```

### Advanced CSS theming

Mount a custom CSS file for full control over the design system:

```yaml
volumes:
  - ./my-theme.css:/app/wwwroot/custom/theme.css
```

### Password reset (SMTP)

In multi-user mode, enable email-based password reset:

```yaml
environment:
  - Desk__smtp__host=smtp.example.com
  - Desk__smtp__port=587
  - Desk__smtp__username=user@example.com
  - Desk__smtp__password=secret
  - Desk__smtp__sender_email=noreply@example.com
```

## Health check

```
GET /health → {"status":"healthy"}
```

## Supported platforms

- `linux/amd64` — Intel/AMD (most cloud servers, Windows with Docker Desktop)
- `linux/arm64` — Apple Silicon, AWS Graviton, Raspberry Pi 4+

The image includes a built-in health check. Use it in orchestrators like Docker Compose, Kubernetes, or Swarm.

## Dashboard vs Desk

Invoicetronic has two web apps:

- **[Dashboard](https://dashboard.invoicetronic.com)** — for developers: manage API keys, webhooks, logs, billing, test/live environments
- **[Desk](https://desk.invoicetronic.com)** — for end users, operators, and ISVs: send, receive, search, upload, and export invoices daily

Most users need both: Dashboard once during setup, Desk every day — eventually alongside your own integrations.

## Links

| | |
|---|---|
| **Desk Cloud** | [desk.invoicetronic.com](https://desk.invoicetronic.com) |
| **Dashboard** | [dashboard.invoicetronic.com](https://dashboard.invoicetronic.com) |
| **Documentation** | [invoicetronic.com/docs](https://invoicetronic.com/en/docs/) |
| **API reference** | [api.invoicetronic.com/v1/docs](https://api.invoicetronic.com/v1/docs) |
| **GitHub** | [github.com/invoicetronic/desk](https://github.com/invoicetronic/desk) |
| **Website** | [invoicetronic.com](https://invoicetronic.com) |

## License

[Apache License 2.0](https://github.com/invoicetronic/desk/blob/main/LICENSE) — use, modify, and redistribute freely, including for commercial purposes.
