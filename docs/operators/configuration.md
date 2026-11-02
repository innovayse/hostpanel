---
title: Configuration reference
description: Every .env setting that Hostpanel reads, with defaults, required flags and the compose tier each setting belongs to.
---

# Configuration reference

## How configuration works

Settings live in `docker/.env.example`, copied to `.env` next to the compose file. The compose file reads from `.env` and passes the values to services as environment variables. Some settings reach services under a different name — for example, `STACK_NETWORK_SUBNET` arrives at the API as `RateLimit__KnownNetworks__0`.

Not every setting lives in `.env.example`. A few rate-limit and order-numbering settings are set directly on the API service in a compose override file (see [Reverse proxy and trusted proxies](#reverse-proxy-and-trusted-proxies) and [API and ports](#api-and-ports) for the complete list).

## Compose files

Each compose file is for a different deployment target. Settings tables below show which tier each setting belongs to.

### How a value reaches the API

The same setting can live in different places depending on whether it is in `.env.example` or set in a compose override. Here is an example using the rate-limit global window:

::: code-group

```bash [.env]
# Most settings live here:
RATE_LIMIT_TRUSTED_PROXY_COUNT=1
STACK_NETWORK_SUBNET=172.30.0.0/24
```

```yaml [compose override]
# Settings not in .env are set directly on the service:
services:
  hostpanel-api:
    environment:
      RateLimit__Global__PermitLimit: 1200
      RateLimit__Global__WindowSeconds: 60
      Orders__NumberPrefix: "ORD-"
```

```text [As the API sees it]
# .env variable         → API configuration key
STACK_NETWORK_SUBNET    → RateLimit__KnownNetworks__0
RATE_LIMIT_TRUSTED_PROXY_COUNT → RateLimit__TrustedProxyCount
RateLimit__Global__*    → set directly, no rename
```

:::

| File | Purpose | Tier |
|---|---|---|
| `docker-compose.hub.yml` | Standalone self-host, used by `install.sh` | hub |
| `docker-compose.shared.yml` + `docker-compose.dev.yml` | Local development | dev |
| `docker-compose.prod.yml` | Deployment built from source | prod |
| `docker-compose.demo.yml` | Demo stack | demo |
| `docker-compose.platform.yml` | Joining an existing network | — |

## Required secrets

<Callout type="danger">
Every `CHANGE_ME` is a placeholder, not a real default. The API refuses to start outside Development if a secret contains a placeholder or is too short. Always generate fresh values for production.
</Callout>

<Callout type="tip">
Generate secure random values using `openssl`:

- **JWT_SECRET** (at least 32 hex characters): `openssl rand -hex 32`
- **ENCRYPTION_KEY** (exactly 32 bytes, base64): `openssl rand -base64 32`
- **SESSION_ENCRYPTION_KEY** (exactly 32 bytes, base64): `openssl rand -base64 32`
- **HOSTPANEL_BACKCHANNEL_LOGOUT_KEY** (at least 16 hex characters): `openssl rand -hex 24`
- **POSTGRES_PASSWORD**: `openssl rand -base64 32`
- **RABBITMQ_PASSWORD**: `openssl rand -base64 32`
</Callout>

## General

<Callout type="warning">
A server left on <code>Development</code> runs the DevDataSeeder and re-seeds test data on every restart. Always set <code>ASPNETCORE_ENVIRONMENT=Production</code> on a deployed host.
</Callout>

<SettingTable :settings="[
  { name: 'ASPNETCORE_ENVIRONMENT', type: 'string', default: 'Development', description: 'Runtime mode: `Production` or `Development`. Development also runs DevDataSeeder, which re-seeds test data on every restart. A server must use `Production`. — *Required in hub and prod. Tiers: all.*' },
  { name: 'AUTH_APP_NAME', type: 'string', default: 'hostpanel', description: 'Application name registered with the auth layer (cookie and client naming). — *Tiers: all.*' },
  { name: 'DISABLE_RELEASE_UPDATE_CHECKS', type: 'flag', default: 'false', description: 'Set to `true` to disable the daily check for a newer release on GitHub. Disable when the installation has no outbound internet access. Maps to `ScheduledJobs__DisableReleaseUpdateChecks`. — *Tiers: hub, prod.*' },
]" />

## Sign-in modes

Hostpanel supports two authentication modes: local (users managed by Hostpanel itself) and SSO (users signed in through an external OpenID Connect provider).

<SettingTable :settings="[
  { name: 'AUTH_MODE', type: 'string', default: 'local', description: '`local`: Hostpanel authenticates its own users. `sso`: sign-in goes to an external OpenID Connect provider. — *Required. Tiers: all.*' },
]" />

### SSO settings

<Label type="sso-only" /> These settings are required only when `AUTH_MODE=sso`.

<SettingTable :settings="[
  { name: 'SSO_URL', type: 'string', default: '', description: 'Server-to-server address the API reaches the provider at. — *Required for SSO. Tiers: all.*' },
  { name: 'SSO_PUBLIC_URL', type: 'string', default: '', description: 'Where the browser is sent to sign in (provider\'s public hostname, issuer). — *Required for SSO. Tiers: all.*' },
  { name: 'SSO_CLIENT_ID', type: 'string', default: 'hostpanel', description: 'Client identifier registered at the provider. — *Required for SSO. Tiers: all.*' },
  { name: 'SSO_CALLBACK_URL', type: 'string', default: '', description: 'URL the provider redirects to after sign-in. The admin callback is `<admin origin>/api/auth/callback`. — *Required for SSO. Tiers: all.*' },
  { name: 'SSO_SERVICE_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Machine credential for reading the user directory. Generate with `openssl rand -base64 24`. — *Required for SSO. Tiers: all.*' },
  { name: 'CLIENT_HOSTPANEL_SECRET', type: 'secret', default: 'CHANGE_ME', description: 'Secret the API exchanges the admin SPA\'s authorization code with, server to server. API refuses to start without it when `AUTH_MODE=sso`. — *Required for SSO. Tiers: all.*' },
  { name: 'HOSTPANEL_BACKCHANNEL_LOGOUT_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Shared with the provider for back-channel logout. Generate with `openssl rand -hex 24`. Must be at least 16 characters on a deployed host. Leave empty only where no provider is connected. — *Required for SSO. Tiers: all.*' },
  { name: 'NUGET_INNOVAYSE_CREDENTIALS', type: 'secret', default: '', description: 'Deploy token for the private NuGet registry. Required only for `AUTH_MODE=sso` builds from source. Never use a personal access token. — *Required for SSO source builds. Tiers: all.*' },
]" />

### Local session settings

<Label type="local-only" /> These settings apply only when `AUTH_MODE=local`.

<SettingTable :settings="[
  { name: 'SESSION_ACCESS_TOKEN_MINUTES', type: 'string', default: '15', description: 'Access-token lifetime in minutes. Sliding renewal re-issues the token while the person keeps working. — *Tiers: all.*' },
  { name: 'SESSION_MAX_MINUTES', type: 'string', default: '720', description: 'Absolute session age after which token renewal stops and the person must sign in again. — *Tiers: all.*' },
]" />

## Addresses and host names

<SettingTable :settings="[
  { name: 'DOMAIN', type: 'string', default: 'yourdomain.com', description: 'Public host name of the client portal (e.g., `panel.example.com`). Must differ from `ADMIN_DOMAIN`. — *Required. Tiers: all.*' },
  { name: 'ADMIN_DOMAIN', type: 'string', default: 'admin.yourdomain.com', description: 'Public host name of the admin panel. Must differ from `DOMAIN`. — *Required. Tiers: all.*' },
  { name: 'ADMIN_BASE_URL', type: 'string', default: 'https://admin.yourdomain.com', description: 'Absolute URL of the admin panel (scheme, no trailing slash). Base of links the API mails to staff. — *Required in prod and hub. Tiers: prod, hub.*' },
  { name: 'CLIENT_BASE_URL', type: 'string', default: 'https://yourdomain.com', description: 'Absolute URL of the client portal (scheme, no trailing slash). Base of links the API mails to clients. — *Required in prod and hub. Tiers: prod, hub.*' },
  { name: 'NUXT_PUBLIC_BASE_URL', type: 'string', default: 'https://panel.example.com', description: 'Public URL the client portal JavaScript uses for API calls. — *Required. Tiers: all.*' },
  { name: 'ADMIN_BASE_URL_PATH', type: 'string', default: '/', description: 'Base path the admin SPA is served under (`/` or `/admin`). Read by the admin container at start, not at build. — *Tiers: all.*' },
  { name: 'ALLOWED_HOSTS', type: 'string', default: 'panel.example.test;hostpanel-api;localhost', description: 'Host names the API answers to, semicolon-separated. Include the public host, `hostpanel-api` and `localhost`. — *Required. Tiers: all.*' },
  { name: 'PANEL_DEV_ORIGIN', type: 'string', default: 'http://panel.example.test', description: 'Dev-proxy origin the API allows alongside localhost (CORS). — *Tiers: dev.*' },
  { name: 'SSH_AGENT_SOURCE_ADDRESSES', type: 'string', default: '', description: 'Public address(es) this installation connects to SSH key agents from, comma-separated. Servers → SSH shows the exact firewall rules. See [SSH key agent](/servers/ssh-key-agent). — *New in next release. Tiers: prod, hub.*' },
  { name: 'PANEL_HOST_ADDRESSES', type: 'string', default: '', description: 'Control panels reachable only over a VPN, as `hostname=address` pairs, comma-separated. — *New in next release. Tiers: prod, hub.*' },
]" />

## Keys and encryption

<Callout type="danger">
`ENCRYPTION_KEY` protects server passwords, API tokens and access hashes. Never change it on a running installation without a re-encryption plan. Back up `.env` with every database dump.
</Callout>

<SettingTable :settings="[
  { name: 'ENCRYPTION_KEY', type: 'secret', default: 'Q0hBTkdF...', description: 'Must decode to exactly 32 bytes. Protects encrypted database columns. Generate: `openssl rand -base64 32`. The example value is a placeholder and is refused outside Development. — *Required in prod, hub, demo. Tiers: prod, hub, demo.*' },
  { name: 'SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Key for client portal sessions (AES-256-GCM). 32 random bytes, base64. Changing it signs everyone out. Required in prod, hub and demo. — *Required in prod, hub, demo. Tiers: prod, hub, demo.*' },
  { name: 'JWT_SECRET', type: 'secret', default: 'CHANGE_ME_...', description: 'At least 32 characters. Placeholder is refused outside Development. Generate: `openssl rand -hex 32`. — *Required. Tiers: all.*' },
  { name: 'JWT_ISSUER', type: 'string', default: 'innovayse-api', description: 'Issuer claim stamped into every token. — *Tiers: all.*' },
  { name: 'JWT_AUDIENCE', type: 'string', default: 'innovayse-clients', description: 'Audience claim verified on every token. — *Tiers: all.*' },
]" />

## Database, message queue and cache

<SettingTable :settings="[
  { name: 'POSTGRES_HOST', type: 'string', default: 'hostpanel-db', description: 'Database host name. — *Tiers: all.*' },
  { name: 'POSTGRES_PORT', type: 'string', default: '5432', description: 'Database port. — *Tiers: all.*' },
  { name: 'POSTGRES_USER', type: 'string', default: 'postgres', description: 'Database user. — *Tiers: all.*' },
  { name: 'POSTGRES_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Database password. Placeholder is refused outside Development. — *Required. Tiers: all.*' },
  { name: 'POSTGRES_DB', type: 'string', default: 'innovayse_dev', description: 'Database name. — *Tiers: all.*' },
  { name: 'RABBITMQ_HOST', type: 'string', default: 'hostpanel-rabbitmq', description: 'Message queue host name. — *Tiers: all.*' },
  { name: 'RABBITMQ_PORT', type: 'string', default: '5672', description: 'Message queue port. — *Tiers: all.*' },
  { name: 'RABBITMQ_USER', type: 'string', default: 'guest', description: 'Message queue user. — *Tiers: all.*' },
  { name: 'RABBITMQ_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Message queue password. Placeholder is refused outside Development. — *Required. Tiers: all.*' },
  { name: 'REDIS_CONNECTION', type: 'string', default: 'hostpanel-redis:6379', description: 'Redis connection string. Used for admin sessions (SSO), data protection key ring, and client portal sign-in sessions. — *Tiers: all.*' },
]" />

## Mail

<Callout type="warning">
Unset or misspelled `SMTP_ENCRYPTION` stops the API on startup and names this key in the log.
</Callout>

<SettingTable :settings="[
  { name: 'SMTP_HOST', type: 'string', default: 'hostpanel-mailhog', description: 'SMTP relay host. Installer leaves this empty; no mail is sent until it is set. — *Required in prod and hub. Tiers: all.*' },
  { name: 'SMTP_PORT', type: 'string', default: '1025', description: 'SMTP port. Usually `587` (STARTTLS) or `465` (implicit TLS). — *Tiers: all.*' },
  { name: 'SMTP_USERNAME', type: 'string', default: '', description: 'Relay username. — *Tiers: all.*' },
  { name: 'SMTP_PASSWORD', type: 'string', default: '', description: 'Relay password. — *Tiers: all.*' },
  { name: 'SMTP_ENCRYPTION', type: 'string', default: 'none', description: '`tls` (STARTTLS), `ssl` (implicit TLS) or `none` (local catcher only). Required and validated on startup. — *Required. Tiers: all.*' },
  { name: 'SMTP_FROM', type: 'string', default: 'noreply@example.com', description: 'Sender address. — *Tiers: all.*' },
  { name: 'SMTP_FROM_NAME', type: 'string', default: 'Hostpanel', description: 'Sender display name. — *Tiers: all.*' },
  { name: 'EMAIL_TO', type: 'string', default: 'contact@yourdomain.com', description: 'Contact form delivery address. Unset → form answers 503 `CONTACT_NOT_CONFIGURED`. — *Tiers: all.*' },
  { name: 'DEPARTMENT_SUPPORT_EMAIL', type: 'string', default: 'support@example.com', description: 'Support department email on first install. Editable in the admin panel afterwards. — *Tiers: all.*' },
  { name: 'DEPARTMENT_BILLING_EMAIL', type: 'string', default: 'billing@example.com', description: 'Billing department email on first install. — *Tiers: all.*' },
  { name: 'DEPARTMENT_SALES_EMAIL', type: 'string', default: 'sales@example.com', description: 'Sales department email on first install. — *Tiers: all.*' },
  { name: 'DEPARTMENT_GENERAL_EMAIL', type: 'string', default: 'hello@example.com', description: 'General department email on first install. — *Tiers: all.*' },
  { name: 'TELEGRAM_BOT_TOKEN', type: 'string', default: '', description: 'Telegram bot token for contact form notifications. Both or neither: half a pair refuses startup. — *Tiers: all.*' },
  { name: 'TELEGRAM_CHAT_ID', type: 'string', default: '', description: 'Telegram chat ID. Both or neither: half a pair refuses startup. — *Tiers: all.*' },
]" />

## Reverse proxy and trusted proxies

<Callout type="warning">
Getting the proxy counts wrong breaks rate limiting and order IP tracking. Too low: every visitor shares one rate-limit partition and orders record the wrong IP. Too high: a visitor can spoof their address.
</Callout>

When the installation sits behind one or more proxies, the API and client portal must trust headers about the real visitor's address. The diagram shows the path:

```
visitor → [extra proxy] → edge (Caddy/nginx) → client portal → API
                                                    ↑
                                         RATE_LIMIT_TRUSTED_PROXY_COUNT
                                         CLIENT_TRUSTED_PROXY_COUNT
```

Each extra proxy in front of the bundled edge adds 1 to both counts. The shipped stacks have one built-in edge layer, so the default is `1`. If you place an extra proxy in front (load balancer, WAF, CDN), increase both counts by 1 per extra layer.

<SettingTable :settings="[
  { name: 'STACK_NETWORK_SUBNET', type: 'string', default: '172.30.0.0/24', description: 'CIDR range of the Docker network the stack creates. Passed to the API as `RateLimit__KnownNetworks__0`. Pick a range no other network on the host uses. — *Required. Tiers: all.*' },
  { name: 'RATE_LIMIT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Number of proxies in front of the API (`RateLimit__TrustedProxyCount`). The shipped stacks need `1` (the bundled edge). Add one per extra proxy. — *Tiers: all.*' },
  { name: 'CLIENT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Number of proxies in front of the client portal (`NUXT_TRUSTED_PROXY_COUNT`). The shipped stacks need `1`. Add one per extra proxy. — *Tiers: all.*' },
]" />

### Rate-limit settings (compose override only)

<Callout type="note">
The following settings are set directly on the API service in a compose override file, not in `.env.example`. Use them to tune rate limiting per endpoint.
</Callout>

| Setting | Default | Description |
|---|---|---|
| `RateLimit__Global__PermitLimit` | `1200` | Requests allowed per window (applies to every request) |
| `RateLimit__Global__WindowSeconds` | `60` | Window length in seconds |
| `RateLimit__Auth__PermitLimit` | `15` | Auth endpoint limit per window |
| `RateLimit__Auth__WindowSeconds` | `60` | Auth window length in seconds |
| `RateLimit__Upstream__PermitLimit` | `60` | Upstream (panel API) limit per window |
| `RateLimit__Upstream__WindowSeconds` | `60` | Upstream window length in seconds |
| `RateLimit__Strict__PermitLimit` | `5` | Strict endpoints limit per window |
| `RateLimit__Strict__WindowSeconds` | `60` | Strict window length in seconds |
| `RateLimit__SshKeys__PermitLimit` | `20` | SSH key endpoints limit per window |
| `RateLimit__SshKeys__WindowSeconds` | `60` | SSH key window length in seconds |
| `RateLimit__ConcurrentPermitLimit` | `5` | Maximum concurrent requests from one address |
| `SignInLockout__MaxFailedAttempts` | `5` | Wrong passwords before lockout |
| `SignInLockout__LockoutMinutes` | `15` | Minutes an account stays locked (locked sign-in → 423) |

## Edge proxy

The edge layer (Caddy on hub, nginx on prod) terminates TLS and routes to the API and client portal.

<SettingTable :settings="[
  { name: 'EDGE_SCHEME', type: 'string', default: 'https://', description: '`https://` for automatic Let\'s Encrypt certificates (default); `http://` when your own proxy terminates TLS (then pick free ports). — *Tiers: hub, prod.*' },
  { name: 'EDGE_HTTP_PORT', type: 'string', default: '80', description: 'HTTP port the edge listens on. — *Tiers: hub, prod.*' },
  { name: 'EDGE_HTTPS_PORT', type: 'string', default: '443', description: 'HTTPS port the edge listens on. — *Tiers: hub, prod.*' },
  { name: 'ACME_EMAIL', type: 'string', default: 'admin@yourdomain.com', description: 'Contact address for Let\'s Encrypt certificates. Set by installer. — *Required for hub. Tiers: hub.*' },
  { name: 'EDGE_MAX_BODY_SIZE', type: 'string', default: '50M', description: 'Largest request body on the client portal host. Use the `M` suffix (e.g., `50M`). — *Tiers: hub, prod.*' },
  { name: 'EDGE_ADMIN_MAX_BODY_SIZE', type: 'string', default: '1M', description: 'Largest request body on the admin host. — *Tiers: hub, prod.*' },
  { name: 'EDGE_CLIENT_CSP_SOURCES', type: 'string', default: '', description: 'Extra origins the client portal\'s CSP allows (space-separated). — *Tiers: hub, prod.*' },
  { name: 'NGINX_HTTP_PORT', type: 'string', default: '80', description: 'Host HTTP port for the bundled nginx (prod stack, no platform overlay). — *Tiers: prod.*' },
  { name: 'NGINX_HTTPS_PORT', type: 'string', default: '443', description: 'Host HTTPS port for the bundled nginx. — *Tiers: prod.*' },
]" />

## Images and versions

<SettingTable :settings="[
  { name: 'IMAGE_TAG', type: 'string', default: 'local', description: 'Release to run, e.g., `1.4.0` or `latest`. No leading `v`. — *Required for hub and prod. Tiers: hub, prod.*' },
  { name: 'DOCKERHUB_NAMESPACE', type: 'string', default: 'innovayse', description: 'Docker Hub user/organisation for hub stack images. — *Tiers: hub.*' },
  { name: 'IMAGE_REGISTRY', type: 'string', default: 'registry.example.com/hostpanel', description: 'Registry path for prod stack images (e.g., `registry.example.com/hostpanel`). — *Required for prod. Tiers: prod.*' },
  { name: 'POSTGRES_IMAGE', type: 'string', default: 'postgres:17.11-alpine3.24@sha256:...', description: 'Full postgres image reference, pinned to a digest. — *Tiers: all.*' },
  { name: 'RABBITMQ_IMAGE', type: 'string', default: 'rabbitmq:3.13.7-...', description: 'Full RabbitMQ image reference, pinned to a digest. — *Tiers: all.*' },
  { name: 'REDIS_IMAGE', type: 'string', default: 'redis:7.4.12-...', description: 'Full Redis image reference, pinned to a digest. — *Tiers: all.*' },
  { name: 'MAILHOG_IMAGE', type: 'string', default: 'mailhog/mailhog:v1.0.1@sha256:...', description: 'Full Mailhog image reference (dev and demo stacks). — *Tiers: dev, demo.*' },
  { name: 'NGINX_IMAGE', type: 'string', default: 'nginxinc/nginx-unprivileged:1.31.7-...', description: 'Full nginx image reference (must be the unprivileged build). — *Tiers: prod.*' },
  { name: 'CADDY_IMAGE', type: 'string', default: 'caddy:2.10.2-alpine@sha256:...', description: 'Full Caddy image reference. — *Tiers: hub.*' },
]" />

## API and ports

<SettingTable :settings="[
  { name: 'API_PORT', type: 'string', default: '5148', description: 'Port the API listens on inside its container. Templated into the edge config — change here only. — *Tiers: all.*' },
  { name: 'CLIENT_PORT', type: 'string', default: '3000', description: 'Port the client portal listens on inside its container. Templated into the edge config — change here only. — *Tiers: all.*' },
  { name: 'API_GC_SERVER', type: 'string', default: '0', description: '.NET GC mode: `0` workstation, `1` server. — *Tiers: prod, hub.*' },
  { name: 'API_GC_CONSERVE_MEMORY', type: 'string', default: '5', description: 'Memory conservation level, 0–9. — *Tiers: prod, hub.*' },
  { name: 'API_GC_HEAP_HARD_LIMIT_PERCENT', type: 'string', default: '75', description: 'Heap cap as a percentage of container memory. — *Tiers: prod, hub.*' },
]" />

### Order numbering and invoice settings (compose override only)

<Callout type="note">
Order numbering and invoice settings are not in `.env`. Set them directly on the API service in a compose override.
</Callout>

| Setting | Default | Description |
|---|---|---|
| `Orders__NumberPrefix` | `ORD-` | Order number prefix |
| `Orders__NumberDigits` | `4` | Zero-padded digits (e.g., `0042`) |
| `Orders__InvoicePaymentTermDays` | `7` | Invoice payment term in days (0–365) |
| `InvoicePdf__MaxLines` | `500` | Maximum line items per PDF |
| `InvoicePdf__MaxConcurrentRenders` | `4` | Concurrent PDF renders |

## Client portal look and content

All these settings control the public-facing storefront and have `NUXT_PUBLIC_PORTAL_*` environment variable fallbacks. The admin panel's **Settings** section wins over environment variables once set.

<SettingTable :settings="[
  { name: 'NUXT_PUBLIC_PORTAL_TEMPLATE', type: 'string', default: 'classic', description: 'Storefront template: `classic`, `aurora` or `nova`. Admin Settings overrides this. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_APPS_ENABLED', type: 'flag', default: 'false', description: 'Show the apps launcher in the header. `true`/`1`/`yes`/`on`. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_EMAIL', type: 'string', default: '', description: 'Footer contact email. Empty hides it. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_TWITTER', type: 'string', default: '', description: 'Twitter/X profile URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_LINKEDIN', type: 'string', default: '', description: 'LinkedIn profile URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_FACEBOOK', type: 'string', default: '', description: 'Facebook profile URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_HOSTING_DEMO_URL', type: 'string', default: '', description: 'Demo site URL linked from the home products section. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', type: 'string', default: '', description: 'Postal address shown on the contact page. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_MAP_URL', type: 'string', default: '', description: 'Map link on the contact page. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_CLIENTS', type: 'string', default: '', description: 'Home page stat: client count (free text). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_UPTIME', type: 'string', default: '', description: 'Home page stat: uptime figure. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_REGIONS', type: 'string', default: '', description: 'Home page stat: regions count. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_HERO_BADGE', type: 'string', default: '', description: 'Short badge text above the home page headline. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_NAME', type: 'string', default: '', description: 'Site name for browser title and social previews. Admin Settings wins. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_TAGLINE', type: 'string', default: '', description: 'Site tagline. Admin Settings wins. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_DEFAULT', type: 'string', default: 'dark', description: 'Default colour mode: `light`, `dark` or `system`. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE', type: 'flag', default: 'true', description: 'Show the light/dark switch. Only the literal `false` hides it. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO', type: 'string', default: '', description: 'Logo URL (light background). Empty uses the built-in mark. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_DARK', type: 'string', default: '', description: 'Logo URL (dark background). Falls back to `NUXT_PUBLIC_PORTAL_LOGO`. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_MARK', type: 'string', default: '', description: 'Square mark for compact spaces. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_FAVICON', type: 'string', default: '', description: 'Favicon URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_PRIMARY', type: 'string', default: '', description: 'Primary brand colour (`#rrggbb`). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_ACCENT', type: 'string', default: '', description: 'Accent brand colour (`#rrggbb`). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING', type: 'string', default: '', description: 'Heading font: `inter`, `noto-sans-armenian`, `noto-serif-armenian` or `system`. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY', type: 'string', default: '', description: 'Body font. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_GTM_ID', type: 'string', default: '', description: 'Google Tag Manager container ID. Waits for cookie consent. Admin Settings wins. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_BASE_URL', type: 'string', default: '', description: 'Live chat server URL. Also a build-time ARG (baked into the image). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN', type: 'string', default: '', description: 'Chat widget token (default language). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_RU', type: 'string', default: '', description: 'Chat widget token (Russian). — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_HY', type: 'string', default: '', description: 'Chat widget token (Armenian). — *Tiers: all.*' },
]" />

## Related applications

Link the client portal to other apps in your platform or third-party services.

<SettingTable :settings="[
  { name: 'MAIN_APP_URL', type: 'string', default: 'https://example.com', description: 'Main site URL shown in the apps launcher. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_SHEETS_URL', type: 'string', default: 'https://sheets.example.com', description: 'Sheets app URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_EMAIL_URL', type: 'string', default: 'https://inbox.example.com', description: 'Email app URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_ERP_URL', type: 'string', default: 'https://erp.example.com', description: 'ERP app URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_TASKS_URL', type: 'string', default: 'https://tasks.example.com', description: 'Tasks app URL. — *Tiers: all.*' },
  { name: 'NUXT_PUBLIC_DRIVE_URL', type: 'string', default: 'https://drive.example.com', description: 'Drive app URL. — *Tiers: all.*' },
  { name: 'BILLING_PORTAL_URL', type: 'string', default: '', description: 'Billing portal address (no trailing slash). Left empty, handoff links are hidden. — *Tiers: all.*' },
  { name: 'STRIPE_PUBLISHABLE_KEY', type: 'string', default: '', description: 'Stripe publishable key for the browser card form. Empty disables the card form. — *Tiers: all.*' },
]" />

## Development-only settings

<Callout type="warning">
The settings in this section are for local development only. Do not use them on servers.
</Callout>

<SettingTable :settings="[
  { name: 'POSTGRES_HOST_PORT', type: 'string', default: '5434', description: 'Host port published by the database container. — *Tiers: dev.*' },
  { name: 'RABBITMQ_HOST_PORT', type: 'string', default: '5673', description: 'Host port published by RabbitMQ. — *Tiers: dev.*' },
  { name: 'RABBITMQ_MANAGEMENT_HOST_PORT', type: 'string', default: '15673', description: 'Host port for the RabbitMQ management UI. — *Tiers: dev.*' },
  { name: 'MAILHOG_SMTP_HOST_PORT', type: 'string', default: '1026', description: 'Host port for the Mailhog SMTP. — *Tiers: dev.*' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'string', default: '8027', description: 'Host port for the Mailhog web UI. — *Tiers: dev.*' },
  { name: 'API_HOST_PORT', type: 'string', default: '5149', description: 'Host port published by the API container. — *Tiers: dev.*' },
  { name: 'CLIENT_HOST_PORT', type: 'string', default: '3001', description: 'Host port published by the client portal. — *Tiers: dev.*' },
  { name: 'ADMIN_HOST_PORT', type: 'string', default: '5174', description: 'Host port published by the admin panel. — *Tiers: dev.*' },
  { name: 'CORS_CLIENT_ORIGIN', type: 'string', default: 'http://localhost:3001', description: 'Browser origin for the client portal (CORS). — *Tiers: dev.*' },
  { name: 'CORS_ADMIN_ORIGIN', type: 'string', default: 'http://localhost:5174', description: 'Browser origin for the admin panel (CORS). — *Tiers: dev.*' },
  { name: 'CORS_STAGE_ORIGIN', type: 'string', default: 'https://stage-panel.example.com', description: 'Browser origin for a staging client portal (CORS). — *Tiers: dev.*' },
  { name: 'CLIENT_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Extra hostnames the client dev server answers to (comma-separated). — *Tiers: dev.*' },
  { name: 'ADMIN_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Extra hostnames the admin dev server answers to (comma-separated). — *Tiers: dev.*' },
  { name: 'DEV_FILE_POLLING', type: 'flag', default: 'true', description: 'Poll bind mounts for changes (`true`/`false`). Needed on Docker Desktop mounts. — *Tiers: dev.*' },
  { name: 'DEV_NUGET_AUDIT', type: 'flag', default: 'false', description: 'Enable NuGet package audit inside the dev API container. — *Tiers: dev.*' },
  { name: 'DEV_NUGET_SIGNATURE_VERIFICATION', type: 'flag', default: 'false', description: 'Enable NuGet signature verification. — *Tiers: dev.*' },
  { name: 'DEV_SEED_ADMIN_PASSWORD', type: 'string', default: 'Admin123!', description: 'Admin account password created by the dev data seeder. Published fixture, not a secret. — *Tiers: dev.*' },
  { name: 'DEV_SEED_CLIENT_PASSWORD', type: 'string', default: 'Test@12345', description: 'Client account password created by the dev data seeder. — *Tiers: dev.*' },
  { name: 'TESTCONTAINERS_HOST_OVERRIDE', type: 'string', default: 'host.docker.internal', description: 'Docker host for Testcontainers in the dev API container. — *Tiers: dev.*' },
  { name: 'ADMIN_API_BASE', type: 'string', default: '/api', description: 'Base path the admin dev server proxies to the API. — *Tiers: dev.*' },
  { name: 'DB_CPUS', type: 'string', default: '1.0', description: 'CPU cap for the database container. — *Tiers: dev.*' },
  { name: 'DB_MEMORY', type: 'string', default: '1G', description: 'Memory cap for the database container. — *Tiers: dev.*' },
  { name: 'RABBITMQ_CPUS', type: 'string', default: '1.0', description: 'CPU cap for RabbitMQ. — *Tiers: dev.*' },
  { name: 'RABBITMQ_MEMORY', type: 'string', default: '512M', description: 'Memory cap for RabbitMQ. — *Tiers: dev.*' },
  { name: 'REDIS_CPUS', type: 'string', default: '0.5', description: 'CPU cap for Redis. — *Tiers: dev.*' },
  { name: 'REDIS_MEMORY', type: 'string', default: '256M', description: 'Memory cap for Redis. — *Tiers: dev.*' },
  { name: 'MAILHOG_CPUS', type: 'string', default: '0.2', description: 'CPU cap for Mailhog. — *Tiers: dev.*' },
  { name: 'MAILHOG_MEMORY', type: 'string', default: '256M', description: 'Memory cap for Mailhog. — *Tiers: dev.*' },
  { name: 'API_CPUS', type: 'string', default: '2.0', description: 'CPU cap for the API. — *Tiers: dev.*' },
  { name: 'API_MEMORY', type: 'string', default: '2G', description: 'Memory cap for the API. — *Tiers: dev.*' },
  { name: 'CLIENT_CPUS', type: 'string', default: '1.5', description: 'CPU cap for the client portal. — *Tiers: dev.*' },
  { name: 'CLIENT_MEMORY', type: 'string', default: '2G', description: 'Memory cap for the client portal. — *Tiers: dev.*' },
  { name: 'ADMIN_CPUS', type: 'string', default: '1.0', description: 'CPU cap for the admin panel. — *Tiers: dev.*' },
  { name: 'ADMIN_MEMORY', type: 'string', default: '1536M', description: 'Memory cap for the admin panel. — *Tiers: dev.*' },
]" />

## Demo mode

<Label type="local-only" /> Demo mode requires `AUTH_MODE=local`.

<Callout type="note">
Demo mode requires `AUTH_MODE=local` and its own `POSTGRES_DB`. It resets sample data on schedule. Set `DEMO_ENABLED=true` to activate it.
</Callout>

<SettingTable :settings="[
  { name: 'DEMO_ENABLED', type: 'flag', default: 'true', description: 'Enable demo mode. — *Tiers: demo.*' },
  { name: 'DEMO_RESET_INTERVAL_HOURS', type: 'string', default: '24', description: 'Hours between database wipes and re-seeds. — *Tiers: demo.*' },
  { name: 'DEMO_ADMIN_EMAIL', type: 'string', default: 'admin@demo.local', description: 'Demo admin sign-in email (shown on the sign-in page). — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_ADMIN_PASSWORD', type: 'string', default: '', description: 'Demo admin password. At least 8 characters with upper and lower case. No default; compose refuses to start without it. — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_CLIENT_EMAIL', type: 'string', default: 'client@demo.local', description: 'Demo client sign-in email. — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_CLIENT_PASSWORD', type: 'string', default: '', description: 'Demo client password. At least 8 characters with upper and lower case. — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_CONTACT_EMAIL', type: 'string', default: '', description: 'Contact email shown in the demo storefront footer. Empty keeps the fictional placeholder. — *Tiers: demo.*' },
  { name: 'DEMO_CONTACT_PHONE', type: 'string', default: '', description: 'Contact phone shown in the demo storefront. — *Tiers: demo.*' },
  { name: 'DEMO_SMTP_PORT', type: 'string', default: '1025', description: 'Port of the demo\'s Mailhog inside the network. — *Tiers: demo.*' },
  { name: 'DEMO_REDIS_PORT', type: 'string', default: '6379', description: 'Port of the demo\'s Redis inside the network. — *Tiers: demo.*' },
  { name: 'DEMO_SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Session encryption key for the demo portal. Use a separate value from the production one. — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_PUBLISH_ADDRESS', type: 'string', default: '127.0.0.1', description: 'Address the demo\'s published ports bind to. Loopback keeps them off the network. — *Required for demo. Tiers: demo.*' },
  { name: 'DEMO_SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'Demo mail catcher encryption. Use `none`. — *Tiers: demo.*' },
  { name: 'DEMO_API_HOST_PORT', type: 'string', default: '5150', description: 'Host port for the demo API. — *Tiers: demo.*' },
  { name: 'DEMO_CLIENT_HOST_PORT', type: 'string', default: '3002', description: 'Host port for the demo client portal. — *Tiers: demo.*' },
  { name: 'DEMO_ADMIN_HOST_PORT', type: 'string', default: '5175', description: 'Host port for the demo admin panel. — *Tiers: demo.*' },
  { name: 'DEMO_ADMIN_BASE_URL', type: 'string', default: 'http://localhost:5175', description: 'Absolute URL of the demo admin panel. — *Tiers: demo.*' },
  { name: 'DEMO_CLIENT_BASE_URL', type: 'string', default: 'http://localhost:3002', description: 'Absolute URL of the demo client portal. — *Tiers: demo.*' },
  { name: 'DEMO_MAILHOG_UI_HOST_PORT', type: 'string', default: '8028', description: 'Host port for the demo Mailhog UI. — *Tiers: demo.*' },
]" />

### Demo network configuration (compose override only)

The demo stack's internal networks are set in a compose override file, not in `.env.example`:

| Setting | Required | Description |
|---|---|---|
| `DEMO_NETWORK_SUBNET` | Yes | CIDR range of the demo stack's internal network |
| `DEMO_EDGE_NETWORK_SUBNET` | Yes | CIDR range of the demo edge network |

## Startup refusals

The API validates secrets and key settings before it accepts any request. If a check fails it logs the exact key and exits. Common causes:

| Symptom in the log | Cause | Fix |
|---|---|---|
| Secret still `CHANGE_ME` or too short | `JWT_SECRET`, `ENCRYPTION_KEY`, `POSTGRES_PASSWORD`, `RABBITMQ_PASSWORD` or a configured `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` holds a placeholder or is too short. | Generate a real value with `openssl rand`. |
| `SMTP_ENCRYPTION` invalid | Value is unset, misspelled or not one of `tls`, `ssl`, `none`. | Set it to the correct value. |
| Only one of an optional pair is set | `TELEGRAM_BOT_TOKEN` set but `TELEGRAM_CHAT_ID` empty, or vice versa. | Set both or clear both. |
| `CLIENT_HOSTPANEL_SECRET` missing | `AUTH_MODE=sso` but `CLIENT_HOSTPANEL_SECRET` is empty. | Set the secret or switch to `AUTH_MODE=local`. |
| `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` too short | Value is shorter than 16 characters on a deployed host. | Generate with `openssl rand -hex 24`. |
| Order number setting invalid | `Orders__NumberPrefix` and `Orders__NumberDigits` cannot produce a valid order number, or `Orders__InvoicePaymentTermDays` is out of range (0–365). | Correct the values in the compose override. |
| `CORS_*` origin invalid | An allowed origin is not a valid absolute URL (e.g. missing scheme). | Fix the origin in `.env`. |

## Error codes

Error codes returned to the client with their HTTP status and message:

| Code | Status | Message |
|---|---|---|
| `CONTACT_NOT_CONFIGURED` | 503 | "The contact form is not available on this deployment. Please use the email address or phone number shown on this page." |
| `SIGN_IN_ACCOUNT_LOCKED` | 423 | "Too many wrong passwords. This account is locked for a while, try again later." |
