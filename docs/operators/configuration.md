---
title: Configuration reference
description: Every .env setting that Hostpanel reads, with defaults, required flags and the compose tier each setting belongs to.
---

# Configuration reference

## How configuration works

Settings live in `docker/.env.example`, copied to `.env` next to the compose file. The compose file reads from `.env` and passes the values to services as environment variables. Some settings reach services under a different name, for example, `STACK_NETWORK_SUBNET` arrives at the API as `RateLimit__KnownNetworks__0`.

Not every setting lives in `.env.example`. A few rate-limit and order-numbering settings are set directly on the API service in a compose override file (see [Reverse proxy and trusted proxies](#reverse-proxy-and-trusted-proxies) and [API and ports](#api-and-ports) for the complete list).

## Compose files

Each compose file is for a different deployment target. Settings tables below show which tier each setting belongs to.

### How a value reaches the API

The same setting can live in different places depending on whether it is in `.env.example` or set in a compose override. Here is an example using the trusted proxy and order settings:

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
      Orders__NumberPrefix: "ORD-"
      Orders__NumberDigits: 4
```

```text [As the API sees it]
# .env variable                   → API configuration key
STACK_NETWORK_SUBNET              → RateLimit__KnownNetworks__0
RATE_LIMIT_TRUSTED_PROXY_COUNT    → RateLimit__TrustedProxyCount
Orders__NumberPrefix (override)   → Orders__NumberPrefix (no rename)
```

:::

| File | Purpose | Tier |
|---|---|---|
| `docker-compose.hub.yml` | Standalone self-host, used by `install.sh` | hub |
| `docker-compose.shared.yml` + `docker-compose.dev.yml` | Local development | dev |
| `docker-compose.prod.yml` | Deployment built from source | prod |
| `docker-compose.demo.yml` | Demo stack | demo |
| `docker-compose.platform.yml` | Joining an existing network | n/a |

## Required secrets

<Callout type="danger">
Every <code>CHANGE_ME</code> is a placeholder, not a real default. The API refuses to start outside Development if a secret contains a placeholder or is too short. Always generate fresh values for production.
</Callout>

<Callout type="tip">
Generate secure random values using <code>openssl</code>:
<ul>
<li><strong>JWT_SECRET</strong> (at least 32 characters): <code>openssl rand -hex 32</code></li>
<li><strong>ENCRYPTION_KEY</strong> (exactly 32 bytes, base64): <code>openssl rand -base64 32</code></li>
<li><strong>SESSION_ENCRYPTION_KEY</strong> (exactly 32 bytes, base64): <code>openssl rand -base64 32</code></li>
<li><strong>HOSTPANEL_BACKCHANNEL_LOGOUT_KEY</strong> (at least 16 characters): <code>openssl rand -hex 24</code></li>
<li><strong>POSTGRES_PASSWORD</strong>: <code>openssl rand -hex 24</code></li>
<li><strong>RABBITMQ_PASSWORD</strong>: <code>openssl rand -hex 24</code></li>
</ul>
</Callout>

## General

<Callout type="warning">
A server left on <code>Development</code> runs the DevDataSeeder and re-seeds test data on every restart. Always set <code>ASPNETCORE_ENVIRONMENT=Production</code> on a deployed host.
</Callout>

<SettingTable :settings="[
  { name: 'ASPNETCORE_ENVIRONMENT', type: 'string', default: 'Development', description: 'Runtime mode: Production or Development. Development also runs DevDataSeeder, which re-seeds test data on every restart. A server must use Production. Required in hub and prod. Tiers: hub, prod, dev.' },
  { name: 'AUTH_APP_NAME', type: 'string', default: 'hostpanel', description: 'Application name registered with the auth layer (cookie and client naming). Tiers: hub, prod, dev.' },
  { name: 'DISABLE_RELEASE_UPDATE_CHECKS', type: 'flag', default: 'false', description: 'Set to true to disable the daily check for a newer release on GitHub. Disable when the installation has no outbound internet access. Maps to ScheduledJobs__DisableReleaseUpdateChecks. Tiers: hub, prod.' },
]" />

## Sign-in modes

Hostpanel supports two authentication modes: local (users managed by Hostpanel itself) and SSO (users signed in through an external OpenID Connect provider).

<SettingTable :settings="[
  { name: 'AUTH_MODE', type: 'string', default: 'local', description: 'local: Hostpanel authenticates its own users. sso: sign-in goes to an external OpenID Connect provider. Required. Tiers: all.' },
]" />

### SSO settings

<Label type="sso" /> These settings are required only when AUTH_MODE=sso.

<SettingTable :settings="[
  { name: 'SSO_URL', type: 'string', default: 'https://sso.example.com', description: 'Server-to-server address the API reaches the provider at. Required for SSO. Tiers: all.' },
  { name: 'SSO_PUBLIC_URL', type: 'string', default: 'https://sso.example.com', description: 'Where the browser is sent to sign in (provider\'s public hostname, issuer). Required for SSO. Tiers: all.' },
  { name: 'SSO_CLIENT_ID', type: 'string', default: 'hostpanel', description: 'Client identifier registered at the provider. Required for SSO. Tiers: all.' },
  { name: 'SSO_CALLBACK_URL', type: 'string', default: 'https://panel.example.com/api/portal/auth/sso/callback', description: 'URL the provider redirects to after sign-in. The admin callback is <admin origin>/api/auth/callback. Required for SSO. Tiers: all.' },
  { name: 'SSO_SERVICE_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Machine credential for reading the user directory. Generate with openssl rand -base64 24. Required for SSO. Tiers: all.' },
  { name: 'CLIENT_HOSTPANEL_SECRET', type: 'secret', default: 'CHANGE_ME', description: 'Secret the API exchanges the admin SPA\'s authorization code with, server to server. API refuses to start without it when AUTH_MODE=sso. Required for SSO. Tiers: all.' },
  { name: 'HOSTPANEL_BACKCHANNEL_LOGOUT_KEY', type: 'secret', default: 'CHANGE_ME', description: 'Shared with the provider for back-channel logout. Generate with openssl rand -hex 24. Must be at least 16 characters on a deployed host. Leave empty only where no provider is connected. Required for SSO. Tiers: all.' },
]" />

Updating to a release that moves the sign-on client package to 5.1 resets the admin panel sessions once: the package now keeps its session cache keys per product, so the sessions stored under the old keys are no longer read. Every administrator signs in again after the update, with no action needed beyond that, and the old entries expire on their own. The package also maps the back-channel logout receiver itself at /api/auth/backchannel-logout, and a sign-in callback that has no matching attempt (a second tab, Back after signing in) now redirects to the sign-in page instead of answering an error body. Nothing needs configuring for either.

### Local session settings

<Label type="local" /> These settings apply only when AUTH_MODE=local.

<SettingTable :settings="[
  { name: 'SESSION_ACCESS_TOKEN_MINUTES', type: 'string', default: '15', description: 'Access-token lifetime in minutes. Sliding renewal re-issues the token while the person keeps working. Tiers: hub, prod, dev.' },
  { name: 'SESSION_MAX_MINUTES', type: 'string', default: '720', description: 'Absolute session age after which token renewal stops and the person must sign in again. Tiers: hub, prod, dev.' },
]" />

## Addresses and host names

<SettingTable :settings="[
  { name: 'DOMAIN', type: 'string', default: 'yourdomain.com', description: 'Public host name of the client portal (e.g., panel.example.com). Must differ from ADMIN_DOMAIN. Required. Tiers: all.' },
  { name: 'ADMIN_DOMAIN', type: 'string', default: 'admin.yourdomain.com', description: 'Public host name of the admin panel. Must differ from DOMAIN. Required. Tiers: all.' },
  { name: 'ADMIN_BASE_URL', type: 'string', default: 'https://admin.yourdomain.com', description: 'Absolute URL of the admin panel (scheme, no trailing slash). Base of links the API mails to staff. Required in prod and hub. Tiers: prod, hub.' },
  { name: 'CLIENT_BASE_URL', type: 'string', default: 'https://yourdomain.com', description: 'Absolute URL of the client portal (scheme, no trailing slash). Base of links the API mails to clients. Required in prod and hub. Tiers: prod, hub.' },
  { name: 'NUXT_PUBLIC_BASE_URL', type: 'string', default: 'https://panel.example.com', description: 'Public URL the client portal JavaScript uses for API calls. Required. Tiers: all.' },
  { name: 'ADMIN_BASE_URL_PATH', type: 'string', default: '/', description: 'Base path the admin SPA is served under (/ or /admin). Read by the admin container at start, not at build. Tiers: hub, prod.' },
  { name: 'ALLOWED_HOSTS', type: 'string', default: 'panel.example.test;hostpanel-api;localhost', description: 'Host names the API answers to, semicolon-separated. Include the public host, hostpanel-api and localhost. Required. Tiers: all.' },
  { name: 'PANEL_DEV_ORIGIN', type: 'string', default: 'http://panel.example.test', description: 'Dev-proxy origin the API allows alongside localhost (CORS). Tiers: dev.' },
  { name: 'SSH_AGENT_SOURCE_ADDRESSES', type: 'string', default: '', description: 'Public address(es) this installation connects to SSH key agents from, comma-separated. New in next release. Tiers: prod, hub.' },
  { name: 'PANEL_HOST_ADDRESSES', type: 'string', default: '', description: 'Control panels reachable only over a VPN, as hostname=address pairs, comma-separated. New in next release. Tiers: prod, hub.' },
]" />

Servers → SSH shows the exact firewall rules once SSH_AGENT_SOURCE_ADDRESSES is set. See [SSH key agent](/servers/ssh-key-agent).

## Keys and encryption

<Callout type="danger">
ENCRYPTION_KEY protects server passwords, API tokens and access hashes. Never change it on a running installation without a re-encryption plan. Back up .env with every database dump.
</Callout>

<SettingTable :settings="[
  { name: 'ENCRYPTION_KEY', type: 'secret', default: 'see .env.example', description: 'Must decode to exactly 32 bytes. Protects encrypted database columns. Generate: openssl rand -base64 32. The example value is a placeholder and is refused outside Development. Required in prod, hub, demo. Tiers: prod, hub, demo.' },
  { name: 'SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Key for client portal sessions (AES-256-GCM). 32 random bytes, base64. Changing it signs everyone out. Required in prod, hub, demo. Tiers: prod, hub, demo.' },
  { name: 'JWT_SECRET', type: 'secret', default: 'see .env.example', description: 'At least 32 characters. Placeholder is refused outside Development. Generate: openssl rand -hex 32. Tiers: all.' },
  { name: 'JWT_ISSUER', type: 'string', default: 'innovayse-api', description: 'Issuer claim stamped into every token. Tiers: all.' },
  { name: 'JWT_AUDIENCE', type: 'string', default: 'innovayse-clients', description: 'Audience claim verified on every token. Tiers: all.' },
]" />

## Database, message queue and cache

<SettingTable :settings="[
  { name: 'POSTGRES_HOST', type: 'string', default: 'hostpanel-db', description: 'Database host name. Tiers: all.' },
  { name: 'POSTGRES_PORT', type: 'string', default: '5432', description: 'Database port. Tiers: all.' },
  { name: 'POSTGRES_USER', type: 'string', default: 'postgres', description: 'Database user. Tiers: all.' },
  { name: 'POSTGRES_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Database password. Placeholder is refused outside Development. Required. Tiers: all.' },
  { name: 'POSTGRES_DB', type: 'string', default: 'innovayse_dev', description: 'Database name. Tiers: all.' },
  { name: 'RABBITMQ_HOST', type: 'string', default: 'hostpanel-rabbitmq', description: 'Message queue host name. Tiers: all.' },
  { name: 'RABBITMQ_PORT', type: 'string', default: '5672', description: 'Message queue port. Tiers: all.' },
  { name: 'RABBITMQ_USER', type: 'string', default: 'guest', description: 'Message queue user. Tiers: all.' },
  { name: 'RABBITMQ_PASSWORD', type: 'secret', default: 'CHANGE_ME', description: 'Message queue password. Placeholder is refused outside Development. Required. Tiers: all.' },
  { name: 'REDIS_CONNECTION', type: 'string', default: 'hostpanel-redis:6379', description: 'Redis connection string. Used for admin sessions (SSO), data protection key ring, and client portal sign-in sessions. Tiers: all.' },
]" />

## Mail

<Callout type="warning">
Unset or misspelled SMTP_ENCRYPTION stops the API on startup and names this key in the log.
</Callout>

<SettingTable :settings="[
  { name: 'SMTP_HOST', type: 'string', default: 'hostpanel-mailhog', description: 'SMTP relay host. Installer leaves this empty; no mail is sent until it is set. Required in prod and hub. Tiers: all.' },
  { name: 'SMTP_PORT', type: 'string', default: '1025', description: 'SMTP port. Usually 587 (STARTTLS) or 465 (implicit TLS). Tiers: all.' },
  { name: 'SMTP_USERNAME', type: 'string', default: '', description: 'Relay username. Tiers: all.' },
  { name: 'SMTP_PASSWORD', type: 'secret', default: '', description: 'Relay password. Tiers: all.' },
  { name: 'SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'tls (STARTTLS), ssl (implicit TLS) or none (local catcher only). Required and validated on startup. Tiers: all.' },
  { name: 'SMTP_FROM', type: 'string', default: 'noreply@example.com', description: 'Sender address. Tiers: all.' },
  { name: 'SMTP_FROM_NAME', type: 'string', default: 'Hostpanel', description: 'Sender display name. Tiers: all.' },
  { name: 'EMAIL_TO', type: 'string', default: 'contact@yourdomain.com', description: 'Contact form delivery address. Unset form answers 503 CONTACT_NOT_CONFIGURED. Tiers: all.' },
  { name: 'DEPARTMENT_SUPPORT_EMAIL', type: 'string', default: 'support@example.com', description: 'Support department email on first install. Editable in the admin panel afterwards. Tiers: all.' },
  { name: 'DEPARTMENT_BILLING_EMAIL', type: 'string', default: 'billing@example.com', description: 'Billing department email on first install. Tiers: all.' },
  { name: 'DEPARTMENT_SALES_EMAIL', type: 'string', default: 'sales@example.com', description: 'Sales department email on first install. Tiers: all.' },
  { name: 'DEPARTMENT_GENERAL_EMAIL', type: 'string', default: 'hello@example.com', description: 'General department email on first install. Tiers: all.' },
  { name: 'TELEGRAM_BOT_TOKEN', type: 'secret', default: '', description: 'Telegram bot token for contact form notifications. Both or neither: half a pair refuses startup. Tiers: all.' },
  { name: 'TELEGRAM_CHAT_ID', type: 'string', default: '', description: 'Telegram chat ID, optional pair. Set both or neither. Tiers: all.' },
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
  { name: 'STACK_NETWORK_SUBNET', type: 'string', default: '172.30.0.0/24', description: 'CIDR range of the Docker network the stack creates. Passed to the API as RateLimit__KnownNetworks__0. Pick a range no other network on the host uses. Required. Tiers: hub, prod, dev.' },
  { name: 'RATE_LIMIT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Number of proxies in front of the API (RateLimit__TrustedProxyCount). The shipped stacks need 1 (the bundled edge). Add one per extra proxy. Tiers: hub, prod, dev.' },
  { name: 'CLIENT_TRUSTED_PROXY_COUNT', type: 'string', default: '1', description: 'Number of proxies in front of the client portal (NUXT_TRUSTED_PROXY_COUNT). The shipped stacks need 1. Add one per extra proxy. The dev stack fixes this at 0 and does not read this variable. Tiers: hub, prod, demo.' },
]" />

### Rate-limit settings (compose override only)

<Callout type="note">
The following settings are set directly on the API service in a compose override file, not in <code>.env.example</code>. Use them to tune rate limiting per endpoint.
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
| `RateLimit__PluginDefault__PermitLimit` | `120` | Plugin route limit per window, for a route that names no tier. Counted per plugin and per caller (the client account, or the administrator on an admin call) |
| `RateLimit__PluginDefault__WindowSeconds` | `60` | Plugin default window length in seconds |
| `RateLimit__PluginStrict__PermitLimit` | `20` | Limit per window for a plugin route that asks for the strict tier (costly or sensitive routes, such as one that sends messages) |
| `RateLimit__PluginStrict__WindowSeconds` | `60` | Plugin strict window length in seconds |
| `RateLimit__PluginHeavy__PermitLimit` | `6` | Limit per window for a plugin route that asks for the heavy tier (reports, exports) |
| `RateLimit__PluginHeavy__WindowSeconds` | `60` | Plugin heavy window length in seconds |
| `RateLimit__PluginCallback__PermitLimit` | `120` | Limit per window for the public plugin callback door (`/api/plugins/{id}/callback/...`). Counted per caller address over every plugin and path, declared or not |
| `RateLimit__PluginCallback__WindowSeconds` | `60` | Plugin callback window length in seconds |
| `RateLimit__PluginStreamPermitLimit` | `3` | Streaming plugin routes one caller may hold open at once, per plugin |
| `SignInLockout__MaxFailedAttempts` | `5` | Wrong passwords before lockout |
| `SignInLockout__LockoutMinutes` | `15` | Minutes an account stays locked (locked sign-in → 423) |

MaxFailedAttempts accepts 1–100, LockoutMinutes accepts 1–1440. Values outside these ranges refuse startup.

Plugin routes are limited per plugin and per caller, so one plugin or one account cannot use up the API. A plugin route declares its tier in the plugin manifest (`rateLimit`: `default`, `strict` or `heavy`); a route that declares none uses the default tier. A refused request answers 429 with `Retry-After` and `X-RateLimit-Remaining`. Every plugin tier must be positive and no larger than `RateLimit__Global__PermitLimit`, the tiers must be ordered heavy <= strict <= default, and `RateLimit__PluginStreamPermitLimit` must be positive, or startup is refused.

The public plugin callback door (`/api/plugins/{id}/callback/...`) is rate limited per caller address, and that address is the one the forwarded headers resolve to. It only works as intended when the forwarded headers are configured: set `RateLimit__TrustedProxyCount` to the number of proxies in front of the API and list them in `RateLimit__KnownProxies` or `RateLimit__KnownNetworks` (the bundled stacks do this through `STACK_NETWORK_SUBNET`). Behind an unconfigured proxy every gateway call shares the proxy's address and one burst refuses all callbacks; with too many trusted hops a sender can write its own `X-Forwarded-For` and pick its own bucket. The API ignores `X-Forwarded-For` from any peer that is not a configured proxy.

## Edge proxy

The edge layer (Caddy on hub, nginx on prod) terminates TLS and routes to the API and client portal.

<SettingTable :settings="[
  { name: 'EDGE_SCHEME', type: 'string', default: 'https://', description: 'https:// for automatic Let\'s Encrypt certificates (default); http:// when your own proxy terminates TLS (then pick free ports). Tiers: hub, prod.' },
  { name: 'EDGE_HTTP_PORT', type: 'string', default: '80', description: 'HTTP port the edge listens on. Tiers: hub, prod.' },
  { name: 'EDGE_HTTPS_PORT', type: 'string', default: '443', description: 'HTTPS port the edge listens on. Tiers: hub, prod.' },
  { name: 'ACME_EMAIL', type: 'string', default: 'admin@yourdomain.com', description: 'Contact address for Let\'s Encrypt certificates. Set by installer. Required for hub. Tiers: hub.' },
  { name: 'EDGE_MAX_BODY_SIZE', type: 'string', default: '50M', description: 'Largest request body on the client portal host. Use the M suffix (e.g., 50M). Tiers: hub, prod.' },
  { name: 'EDGE_ADMIN_MAX_BODY_SIZE', type: 'string', default: '1M', description: 'Largest request body on the admin host. Tiers: hub, prod.' },
  { name: 'EDGE_CLIENT_CSP_SOURCES', type: 'string', default: '', description: 'Extra origins the client portal\'s CSP allows (space-separated). Add the script and API origins a payment plugin\'s checkout needs; the default policy names no gateway. Tiers: hub, prod.' },
  { name: 'NGINX_HTTP_PORT', type: 'string', default: '80', description: 'Host HTTP port for the bundled nginx (prod stack, no platform overlay). Tiers: prod.' },
  { name: 'NGINX_HTTPS_PORT', type: 'string', default: '443', description: 'Host HTTPS port for the bundled nginx. Tiers: prod.' },
]" />

## Images and versions

<SettingTable :settings="[
  { name: 'IMAGE_TAG', type: 'string', default: 'local', description: 'Release to run, e.g., 1.4.0 or latest. No leading v. Required for hub and prod. Tiers: hub, prod.' },
  { name: 'DOCKERHUB_NAMESPACE', type: 'string', default: 'innovayse', description: 'Docker Hub user/organisation for hub stack images. Tiers: hub.' },
  { name: 'IMAGE_REGISTRY', type: 'string', default: 'registry.example.com/hostpanel', description: 'Registry path for prod stack images (e.g., registry.example.com/hostpanel). Required for prod. Tiers: prod.' },
  { name: 'POSTGRES_IMAGE', type: 'string', default: 'see .env.example', description: 'Full postgres image reference, pinned to a digest. Tiers: all.' },
  { name: 'RABBITMQ_IMAGE', type: 'string', default: 'see .env.example', description: 'Full RabbitMQ image reference, pinned to a digest. Tiers: all.' },
  { name: 'REDIS_IMAGE', type: 'string', default: 'see .env.example', description: 'Full Redis image reference, pinned to a digest. Tiers: all.' },
  { name: 'MAILHOG_IMAGE', type: 'string', default: 'see .env.example', description: 'Full Mailhog image reference (dev and demo stacks). Tiers: dev, demo.' },
  { name: 'NGINX_IMAGE', type: 'string', default: 'see .env.example', description: 'Full nginx image reference (must be the unprivileged build). Tiers: prod.' },
  { name: 'CADDY_IMAGE', type: 'string', default: 'see .env.example', description: 'Full Caddy image reference. Tiers: hub.' },
]" />

## API and ports

<SettingTable :settings="[
  { name: 'API_PORT', type: 'string', default: '5148', description: 'Port the API listens on inside its container. Templated into the edge config, change here only. Tiers: all.' },
  { name: 'CLIENT_PORT', type: 'string', default: '3000', description: 'Port the client portal listens on inside its container. Templated into the edge config, change here only. Tiers: all.' },
  { name: 'API_GC_SERVER', type: 'string', default: '0', description: '.NET GC mode: 0 workstation, 1 server. Tiers: prod, hub.' },
  { name: 'API_GC_CONSERVE_MEMORY', type: 'string', default: '5', description: 'Memory conservation level, 0–9. Tiers: prod, hub.' },
  { name: 'API_GC_HEAP_HARD_LIMIT_PERCENT', type: 'string', default: '75', description: 'Heap cap as a percentage of container memory. Tiers: prod, hub.' },
]" />

### Order numbering and invoice settings (compose override only)

<Callout type="note">
Order numbering and invoice settings are not in <code>.env</code>. Set them directly on the API service in a compose override.
</Callout>

| Setting | Default | Description |
|---|---|---|
| `Orders__NumberPrefix` | `ORD-` | Order number prefix |
| `Orders__NumberDigits` | `4` | Zero-padded digits (e.g., `0042`) |
| `Orders__InvoicePaymentTermDays` | `7` | Invoice payment term in days (0–365) |
| `InvoicePdf__MaxLines` | `500` | Maximum line items per PDF |
| `InvoicePdf__MaxConcurrentRenders` | `4` | Concurrent PDF renders |
| `InvoicePdf__MaxRenderWaitSeconds` | `10` | Seconds a download waits for a free render slot before answering 503 with Retry-After. |

## Client portal look and content

All these settings control the public-facing storefront and have NUXT_PUBLIC_PORTAL_* environment variable fallbacks. The admin panel's Settings section wins over environment variables once set.

<SettingTable :settings="[
  { name: 'NUXT_PUBLIC_PORTAL_TEMPLATE', type: 'string', default: 'classic', description: 'Storefront template: classic, aurora or nova. Admin Settings overrides this. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_APPS_ENABLED', type: 'flag', default: 'false', description: 'Show the apps launcher in the header. true/1/yes/on. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_EMAIL', type: 'string', default: '', description: 'Footer contact email. Empty hides it. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_TWITTER', type: 'string', default: '', description: 'Twitter/X profile URL. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_LINKEDIN', type: 'string', default: '', description: 'LinkedIn profile URL. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_SOCIAL_FACEBOOK', type: 'string', default: '', description: 'Facebook profile URL. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_HOSTING_DEMO_URL', type: 'string', default: '', description: 'Demo site URL linked from the home products section. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_ADDRESS', type: 'string', default: '', description: 'Postal address shown on the contact page. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CONTACT_MAP_URL', type: 'string', default: '', description: 'Map link on the contact page. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_CLIENTS', type: 'string', default: '', description: 'Home page stat: client count (free text). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_UPTIME', type: 'string', default: '', description: 'Home page stat: uptime figure. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_STATS_REGIONS', type: 'string', default: '', description: 'Home page stat: regions count. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_HERO_BADGE', type: 'string', default: '', description: 'Short badge text above the home page headline. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_NAME', type: 'string', default: '', description: 'Site name for browser title and social previews. Admin Settings wins. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_SITE_TAGLINE', type: 'string', default: '', description: 'Site tagline. Admin Settings wins. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_DEFAULT', type: 'string', default: 'dark', description: 'Default colour mode: light, dark or system. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_THEME_USER_TOGGLE', type: 'flag', default: 'true', description: 'Show the light/dark switch. Only the literal false hides it. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO', type: 'string', default: '', description: 'Logo URL (light background). Empty uses the built-in mark. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_DARK', type: 'string', default: '', description: 'Logo URL (dark background). Falls back to NUXT_PUBLIC_PORTAL_LOGO. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_LOGO_MARK', type: 'string', default: '', description: 'Square mark for compact spaces. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_FAVICON', type: 'string', default: '', description: 'Favicon URL. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_PRIMARY', type: 'string', default: '', description: 'Primary brand colour (#rrggbb). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_ACCENT', type: 'string', default: '', description: 'Accent brand colour (#rrggbb). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_HEADING', type: 'string', default: '', description: 'Heading font: inter, noto-sans-armenian, noto-serif-armenian or system. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_BRAND_FONT_BODY', type: 'string', default: '', description: 'Body font. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_GTM_ID', type: 'string', default: '', description: 'Google Tag Manager container ID. Waits for cookie consent. Admin Settings wins. Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_BASE_URL', type: 'string', default: '', description: 'Live chat server URL. Also a build-time ARG (baked into the image). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN', type: 'string', default: '', description: 'Chat widget token (default language). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_RU', type: 'string', default: '', description: 'Chat widget token (Russian). Tiers: all.' },
  { name: 'NUXT_PUBLIC_PORTAL_CHAT_WEBSITE_TOKEN_HY', type: 'string', default: '', description: 'Chat widget token (Armenian). Tiers: all.' },
]" />

## Payments and external links

<SettingTable :settings="[
  { name: 'BILLING_PORTAL_URL', type: 'string', default: '', description: 'Billing portal address (no trailing slash). Left empty, handoff links are hidden. Tiers: all.' },
]" />


## Plugins

<Callout type="note">
Plugin settings are not in .env. Set them on the API service in a compose override.
</Callout>

| Setting | Default | Description |
|---|---|---|
| `Plugins__InstallPath` | *(empty, falls back to `plugins-installed` beside the app)* | Directory where the API installs plugin packages. |
| `Plugins__MaxEntryCount` | `1000` | Maximum number of files in a plugin zip archive. |
| `Plugins__MaxUncompressedBytes` | `209715200` | Maximum total uncompressed size of a plugin archive in bytes (200 MiB). |
| `Plugins__MigrationTimeoutSeconds` | `120` | Longest one plugin migration hook may run after an upgrade, in seconds (1 to 3600). Past it the host stops waiting and keeps the plugin switched off until a later attempt succeeds. |
| `Plugins__MaxViewBytes` | `262144` | Maximum size in bytes of one plugin view document (a page or slot `view` file, 256 KiB); a larger file refuses the plugin at load and at install. 1 to 4194304 (4 MiB). |
| `Plugins__TrustedPackageSha256__0`, `__1`, ... | n/a | SHA-256 hashes of plugin packages to trust unconditionally. Each hash is a separate indexed key. |
| `PluginApi__MaxBodyBytes` | `1048576` | Largest request body the host reads for a plugin route, in bytes (1 to 67108864). |
| `PluginApi__CallTimeoutSeconds` | `30` | Seconds the host waits for a plugin route to answer (1 to the ceiling below). Past it the plugin is cancelled through its token and the caller gets `PLUGIN_TIMEOUT` (504). On a streamed reply it covers the wait for the reply and for the first event and the silence between events, never the length of a stream that keeps sending. |
| `PluginApi__MaxCallTimeoutSeconds` | `600` | The ceiling for a per route limit (1 to 3600). Every route gets `CallTimeoutSeconds` today; a route will be able to ask for a longer limit, up to this, once the plugin SDK has a field for it. Must be at least `CallTimeoutSeconds`. |
| `PluginApi__AbandonWaitSeconds` | `5` | Seconds, after the call limit, the host waits for a plugin that ignores its cancellation before it abandons the call (1 to 60). The caller gets `PLUGIN_TIMEOUT` either way. |
| `PluginApi__MaxAbandonedCallsPerPlugin` | `8` | Most abandoned calls of one plugin that may still be running (1 to 1000). At the limit, new calls to that plugin are refused at once with `PLUGIN_TIMEOUT` until one of them ends. |
| `PluginApi__StreamHeartbeatSeconds` | `15` | Seconds a streamed plugin reply may stay silent before the host writes a comment line to keep the connection alive (1 to 300). |
| `PluginApi__StreamEventMaxBytes` | `65536` | Largest data of one event of a streamed plugin reply, in bytes (1 to 1048576). A plugin that sends a larger event ends the stream with an `error` event. |
| `PluginSigning__MaintainerKeys__0__KeyId`, `__PublicKey`, ... | *(shipped in the image)* | Ed25519 signing key entries for the official plugin maintainers. Shipped in the image; operators do not set these. |
| `PluginSigning__OperatorKeys__0__KeyId` | n/a | Key ID of an operator-trusted plugin signer. Each entry is a pair of `__KeyId` and `__PublicKey` under the same index. |
| `PluginSigning__OperatorKeys__0__PublicKey` | n/a | Ed25519 public key of the operator-trusted signer at index 0. Add more signers as `__1__KeyId` / `__1__PublicKey`, and so on. |

## Development-only settings

<Callout type="warning">
The settings in this section are for local development only. Do not use them on servers.
</Callout>

<SettingTable :settings="[
  { name: 'POSTGRES_HOST_PORT', type: 'string', default: '5434', description: 'Host port published by the database container. Tiers: dev.' },
  { name: 'RABBITMQ_HOST_PORT', type: 'string', default: '5673', description: 'Host port published by RabbitMQ. Tiers: dev.' },
  { name: 'RABBITMQ_MANAGEMENT_HOST_PORT', type: 'string', default: '15673', description: 'Host port for the RabbitMQ management UI. Tiers: dev.' },
  { name: 'MAILHOG_SMTP_HOST_PORT', type: 'string', default: '1026', description: 'Host port for the Mailhog SMTP. Tiers: dev.' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'string', default: '8027', description: 'Host port for the Mailhog web UI. Tiers: dev.' },
  { name: 'API_HOST_PORT', type: 'string', default: '5149', description: 'Host port published by the API container. Tiers: dev.' },
  { name: 'CLIENT_HOST_PORT', type: 'string', default: '3001', description: 'Host port published by the client portal. Tiers: dev.' },
  { name: 'ADMIN_HOST_PORT', type: 'string', default: '5174', description: 'Host port published by the admin panel. Tiers: dev.' },
  { name: 'CORS_CLIENT_ORIGIN', type: 'string', default: 'http://localhost:3001', description: 'Browser origin for the client portal (CORS). Tiers: dev.' },
  { name: 'CORS_ADMIN_ORIGIN', type: 'string', default: 'http://localhost:5174', description: 'Browser origin for the admin panel (CORS). Tiers: dev.' },
  { name: 'CORS_STAGE_ORIGIN', type: 'string', default: 'https://stage-panel.example.com', description: 'Browser origin for a staging client portal (CORS). Tiers: dev.' },
  { name: 'CLIENT_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Extra hostnames the client dev server answers to (comma-separated). Tiers: dev.' },
  { name: 'ADMIN_DEV_ALLOWED_HOSTS', type: 'string', default: '', description: 'Extra hostnames the admin dev server answers to (comma-separated). Tiers: dev.' },
  { name: 'DEV_FILE_POLLING', type: 'flag', default: 'true', description: 'Poll bind mounts for changes (true/false). Needed on Docker Desktop mounts. Tiers: dev.' },
  { name: 'DEV_NUGET_AUDIT', type: 'flag', default: 'false', description: 'Enable NuGet package audit inside the dev API container. Tiers: dev.' },
  { name: 'DEV_NUGET_SIGNATURE_VERIFICATION', type: 'flag', default: 'false', description: 'Enable NuGet signature verification. Tiers: dev.' },
  { name: 'DEV_SEED_ADMIN_PASSWORD', type: 'string', default: 'Admin123!', description: 'Admin account password created by the dev data seeder. Published fixture, not a secret. Tiers: dev.' },
  { name: 'DEV_SEED_CLIENT_PASSWORD', type: 'string', default: 'Test@12345', description: 'Client account password created by the dev data seeder. Tiers: dev.' },
  { name: 'TESTCONTAINERS_HOST_OVERRIDE', type: 'string', default: 'host.docker.internal', description: 'Docker host for Testcontainers in the dev API container. Tiers: dev.' },
  { name: 'ADMIN_API_BASE', type: 'string', default: '/api', description: 'Base path the admin dev server proxies to the API. Tiers: dev.' },
  { name: 'DB_CPUS', type: 'string', default: '1.0', description: 'CPU cap for the database container. Tiers: dev.' },
  { name: 'DB_MEMORY', type: 'string', default: '1G', description: 'Memory cap for the database container. Tiers: dev.' },
  { name: 'RABBITMQ_CPUS', type: 'string', default: '1.0', description: 'CPU cap for RabbitMQ. Tiers: dev.' },
  { name: 'RABBITMQ_MEMORY', type: 'string', default: '512M', description: 'Memory cap for RabbitMQ. Tiers: dev.' },
  { name: 'REDIS_CPUS', type: 'string', default: '0.5', description: 'CPU cap for Redis. Tiers: dev.' },
  { name: 'REDIS_MEMORY', type: 'string', default: '256M', description: 'Memory cap for Redis. Tiers: dev.' },
  { name: 'MAILHOG_CPUS', type: 'string', default: '0.2', description: 'CPU cap for Mailhog. Tiers: dev.' },
  { name: 'MAILHOG_MEMORY', type: 'string', default: '256M', description: 'Memory cap for Mailhog. Tiers: dev.' },
  { name: 'API_CPUS', type: 'string', default: '2.0', description: 'CPU cap for the API. Tiers: dev.' },
  { name: 'API_MEMORY', type: 'string', default: '2G', description: 'Memory cap for the API. Tiers: dev.' },
  { name: 'CLIENT_CPUS', type: 'string', default: '1.5', description: 'CPU cap for the client portal. Tiers: dev.' },
  { name: 'CLIENT_MEMORY', type: 'string', default: '2G', description: 'Memory cap for the client portal. Tiers: dev.' },
  { name: 'ADMIN_CPUS', type: 'string', default: '1.0', description: 'CPU cap for the admin panel. Tiers: dev.' },
  { name: 'ADMIN_MEMORY', type: 'string', default: '1536M', description: 'Memory cap for the admin panel. Tiers: dev.' },
]" />

## Demo mode

<Label type="local" /> Demo mode requires AUTH_MODE=local.

<Callout type="note">
Demo mode requires AUTH_MODE=local and its own POSTGRES_DB. It resets sample data on schedule. Set DEMO_ENABLED=true to activate it.
</Callout>

<SettingTable :settings="[
  { name: 'DEMO_ENABLED', type: 'flag', default: 'true', description: 'Enable demo mode. Tiers: demo.' },
  { name: 'DEMO_RESET_INTERVAL_HOURS', type: 'string', default: '24', description: 'Hours between database wipes and re-seeds. Tiers: demo.' },
  { name: 'DEMO_ADMIN_EMAIL', type: 'string', default: 'admin@demo.local', description: 'Demo admin sign-in email (shown on the sign-in page). Tiers: demo.' },
  { name: 'DEMO_ADMIN_PASSWORD', type: 'secret', default: '', description: 'Demo admin password. At least 8 characters with upper and lower case. No default; compose refuses to start without it. Required for demo. Tiers: demo.' },
  { name: 'DEMO_CLIENT_EMAIL', type: 'string', default: 'client@demo.local', description: 'Demo client sign-in email. Tiers: demo.' },
  { name: 'DEMO_CLIENT_PASSWORD', type: 'secret', default: '', description: 'Demo client password. At least 8 characters with upper and lower case. Required for demo. Tiers: demo.' },
  { name: 'DEMO_CONTACT_EMAIL', type: 'string', default: '', description: 'Contact email shown in the demo storefront footer. Empty keeps the fictional placeholder. Tiers: demo.' },
  { name: 'DEMO_CONTACT_PHONE', type: 'string', default: '', description: 'Contact phone shown in the demo storefront. Tiers: demo.' },
  { name: 'DEMO_SMTP_PORT', type: 'string', default: '1025', description: 'Port of the demo\'s Mailhog inside the network. Tiers: demo.' },
  { name: 'DEMO_REDIS_PORT', type: 'string', default: '6379', description: 'Port of the demo\'s Redis inside the network. Tiers: demo.' },
  { name: 'DEMO_SESSION_ENCRYPTION_KEY', type: 'secret', default: '', description: 'Session encryption key for the demo portal. Use a separate value from the production one. Required for demo. Tiers: demo.' },
  { name: 'DEMO_PUBLISH_ADDRESS', type: 'string', default: '127.0.0.1', description: 'Address the demo\'s published ports bind to. Loopback keeps them off the network. Required for demo. Tiers: demo.' },
  { name: 'DEMO_SMTP_ENCRYPTION', type: 'string', default: 'none', description: 'Demo mail catcher encryption. Use none. Required for demo. Tiers: demo.' },
  { name: 'DEMO_API_HOST_PORT', type: 'string', default: '5150', description: 'Host port for the demo API. Required for demo. Tiers: demo.' },
  { name: 'DEMO_CLIENT_HOST_PORT', type: 'string', default: '3002', description: 'Host port for the demo client portal. Required for demo. Tiers: demo.' },
  { name: 'DEMO_ADMIN_HOST_PORT', type: 'string', default: '5175', description: 'Host port for the demo admin panel. Required for demo. Tiers: demo.' },
  { name: 'DEMO_ADMIN_BASE_URL', type: 'string', default: 'http://localhost:5175', description: 'Absolute URL of the demo admin panel. Required for demo. Tiers: demo.' },
  { name: 'DEMO_CLIENT_BASE_URL', type: 'string', default: 'http://localhost:3002', description: 'Absolute URL of the demo client portal. Required for demo. Tiers: demo.' },
  { name: 'DEMO_MAILHOG_UI_HOST_PORT', type: 'string', default: '8028', description: 'Host port for the demo Mailhog UI. Tiers: demo.' },
  { name: 'DEMO_NETWORK_SUBNET', type: 'string', default: '', description: 'Required. CIDR range of the demo stack internal network. Not in .env.example; docker-compose.demo.yml refuses to start without it. Example: 172.28.0.0/16. Tiers: demo.' },
  { name: 'DEMO_EDGE_NETWORK_SUBNET', type: 'string', default: '', description: 'Required. CIDR range of the demo edge network. Not in .env.example; docker-compose.demo.yml refuses to start without it. Example: 172.29.0.0/16. Tiers: demo.' },
]" />

## Startup refusals

The API validates secrets and key settings before it accepts any request. If a check fails it logs the exact key and exits. Common causes:

| Symptom in the log | Cause | Fix |
|---|---|---|
| Secret still CHANGE_ME or too short | JWT_SECRET, ENCRYPTION_KEY, POSTGRES_PASSWORD, RABBITMQ_PASSWORD or a configured HOSTPANEL_BACKCHANNEL_LOGOUT_KEY holds a placeholder or is too short. | Generate a real value with openssl rand. |
| SMTP_ENCRYPTION invalid | Value is unset, misspelled or not one of tls, ssl, none. | Set it to the correct value. |
| Only one of an optional pair is set | TELEGRAM_BOT_TOKEN set but TELEGRAM_CHAT_ID empty, or vice versa. | Set both or clear both. |
| CLIENT_HOSTPANEL_SECRET missing | AUTH_MODE=sso but CLIENT_HOSTPANEL_SECRET is empty. | Set the secret or switch to AUTH_MODE=local. |
| HOSTPANEL_BACKCHANNEL_LOGOUT_KEY too short | Value is shorter than 16 characters on a deployed host. | Generate with openssl rand -hex 24. |
| Order number setting invalid | Orders__NumberPrefix and Orders__NumberDigits cannot produce a valid order number, or Orders__InvoicePaymentTermDays is out of range (0–365). | Correct the values in the compose override. |
| CORS_* origin invalid | An allowed origin is not a valid absolute URL (e.g. missing scheme). | Fix the origin in .env. |

## Error codes

Error codes returned to the client with their HTTP status and message:

| Code | Status | Message |
|---|---|---|
| `CONTACT_NOT_CONFIGURED` | 503 | "The contact form is not available on this deployment. Please use the email address or phone number shown on this page." |
| `SIGN_IN_ACCOUNT_LOCKED` | 423 | "Too many wrong passwords. This account is locked for a while, try again later." |
