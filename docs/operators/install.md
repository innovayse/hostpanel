---
title: Install Hostpanel
description: Step-by-step guide to installing Hostpanel on a Linux server using the one-command installer or docker compose.
---

# Install Hostpanel

How to get Hostpanel running on your server with the one-command installer or
manually with docker compose.

## Before you start

You need:

- A Linux server with [Docker](https://docs.docker.com/engine/install/) and the
  [Compose v2 plugin](https://docs.docker.com/compose/install/) installed and the
  daemon running. The installer checks for `docker`, `docker compose`, `curl` and
  `openssl` and names any that are missing.
- Two DNS host names pointing at your server — one for the client portal
  (e.g. `panel.example.com`) and one for the admin panel (e.g. `admin.example.com`).
  They must be different: the admin panel is served from the root path and needs its
  own host.
- An email address for the administrator account, the contact form target and the
  Let's Encrypt certificate.

Unless you use [`--no-tls`](#without-tls), ports **80** and **443** must be free and
reachable on the server before you run the installer.

## Run the installer

::: code-group

```bash [install.sh]
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash
```

```bash [docker compose]
# Download the three files, fill .env, then:
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

```bash [.env]
# Required values for a manual install (generate secrets with openssl):
ASPNETCORE_ENVIRONMENT=Production
AUTH_MODE=local
IMAGE_TAG=latest
DOMAIN=panel.example.com
ADMIN_DOMAIN=admin.example.com
ADMIN_BASE_URL=https://panel.example.com
CLIENT_BASE_URL=https://panel.example.com
NUXT_PUBLIC_BASE_URL=https://panel.example.com
ALLOWED_HOSTS=panel.example.com;admin.example.com;hostpanel-api;localhost
ACME_EMAIL=you@example.com
EMAIL_TO=you@example.com
SMTP_HOST=smtp.example.com
SMTP_ENCRYPTION=tls
POSTGRES_DB=hostpanel
POSTGRES_PASSWORD=<openssl rand -hex 24>
RABBITMQ_PASSWORD=<openssl rand -hex 24>
JWT_SECRET=<openssl rand -hex 32>
ENCRYPTION_KEY=<openssl rand -base64 32>
SESSION_ENCRYPTION_KEY=<openssl rand -base64 32>
HOSTPANEL_BACKCHANNEL_LOGOUT_KEY=<openssl rand -hex 24>
```

:::

The one-command installer is recommended — it generates all secrets, sets every
required value and waits for the stack to become healthy. The docker compose and
`.env` tabs are for operators who need a scripted or custom setup; see
[Install by hand with docker compose](#install-by-hand-with-docker-compose) for the
full steps.

**1.** Point the DNS of both host names at your server and wait for them to propagate.

**2. Run the installer:**

```bash
curl -fsSL \
  https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | bash
```

**3. Answer the three prompts:**

| Prompt | Example |
|---|---|
| `Domain of the client portal (e.g. panel.example.com)` | `panel.example.com` |
| `Domain of the admin panel` (default `admin.<domain>`) | `admin.example.com` |
| `Admin e-mail (contact form target and mail sender)` | `you@example.com` |

**4.** The installer pulls the images, starts the stack and waits for `/health`.
When it finishes it prints both URLs and a one-time setup token:

```
Hostpanel is running.

  Client portal : https://panel.example.com
  Admin panel   : https://admin.example.com
  Install dir   : /opt/hostpanel  (secrets are in .env, mode 600)

  Certificates are issued by Let's Encrypt on first request.
  DNS for both hosts must point at this server and ports 80/443
  must be open; watch it with:
    cd /opt/hostpanel
    docker compose -f docker-compose.hub.yml logs hostpanel-caddy

First admin account
  1. Open https://admin.example.com and create your account.
  2. Paste this one-time setup token when asked:

       <setup-token>

  Lost it? Read it from the API log:
    cd /opt/hostpanel
    docker compose -f docker-compose.hub.yml logs hostpanel-api \
      | grep "setup token"

Next: set a real SMTP relay in /opt/hostpanel/.env, then:
  cd /opt/hostpanel
  docker compose -f docker-compose.hub.yml up -d
Upgrade any time by running the install command again.
```

**5.** Open the admin panel URL, create your account and paste the setup token.
See [First run](#first-run).

## Installer options

Pass flags on the command line or set the matching environment variable in front of
`bash` on the pipe:

<SettingTable :settings="[
  { name: '--dir &lt;path&gt; / HOSTPANEL_DIR', type: 'string', default: '/opt/hostpanel', description: 'Directory the installer downloads files into and starts the stack from.' },
  { name: '--version &lt;x.y.z&gt; / HOSTPANEL_VERSION', type: 'string', default: 'latest', description: 'Release to install, e.g. 1.4.0. A leading v is accepted. Omit for the latest release.' },
  { name: '--no-tls / HOSTPANEL_TLS=off', type: 'flag', default: 'on', description: 'Serve plain HTTP on port 8080 behind your own TLS proxy instead of getting automatic certificates. See Without TLS.' },
  { name: '--help / -h', type: 'flag', default: '—', description: 'Print usage and exit.' },
  { name: 'HOSTPANEL_NONINTERACTIVE=1', type: 'env', default: '—', description: 'Skip the interactive prompts. Requires HOSTPANEL_DOMAIN and HOSTPANEL_ADMIN_EMAIL to be set.' },
  { name: 'HOSTPANEL_DOMAIN', type: 'env', default: '—', description: 'Public host name of the client portal. Required for a non-interactive run.' },
  { name: 'HOSTPANEL_ADMIN_DOMAIN', type: 'env', default: 'admin.&lt;domain&gt;', description: 'Public host name of the admin panel.' },
  { name: 'HOSTPANEL_ADMIN_EMAIL', type: 'env', default: '—', description: 'Administrator email address. Required for a non-interactive run.' },
]" />

**Non-interactive example** (CI/CD or a provisioning script):

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh \
  | HOSTPANEL_DOMAIN=panel.example.com \
    HOSTPANEL_ADMIN_EMAIL=you@example.com \
    HOSTPANEL_NONINTERACTIVE=1 \
    bash
```

## With automatic TLS (default)

The bundled [Caddy](https://caddyserver.com/) edge proxy requests Let's Encrypt
certificates for both host names on first request. `ACME_EMAIL` is set to the
admin email you entered.

DNS for both hosts **must point at your server** before you run the installer, or
the certificate request will fail. Watch the edge logs with:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs -f hostpanel-caddy
```

## Without TLS

<Callout type="note">
Use <code>--no-tls</code> when your server already has a TLS proxy (nginx, Caddy, a load
balancer) in front of it. That proxy handles HTTPS; the bundled edge serves plain HTTP on
port 8080.
</Callout>

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash -s -- --no-tls
```

`--no-tls` sets three values in `.env`:

| Setting | Value |
|---|---|
| `EDGE_SCHEME` | `http://` |
| `EDGE_HTTP_PORT` | `8080` |
| `EDGE_HTTPS_PORT` | `8443` |

Your proxy must forward **both** host names to port 8080, keep the original `Host`
header and add `X-Forwarded-Proto: https` — sign-in cookies require a secure origin.

Each extra proxy in front of the stack raises `RATE_LIMIT_TRUSTED_PROXY_COUNT` and
`CLIENT_TRUSTED_PROXY_COUNT` by one. See the [configuration reference](/operators/configuration)
for details.

## What the installer writes

The installer downloads three files into the install directory:

- `docker-compose.hub.yml` — the stack definition
- `Caddyfile` — the edge proxy configuration
- `.env.example` — the settings template

It then creates `.env` (mode `600`) from `.env.example` with these values set:

| Setting | Value written |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `AUTH_MODE` | `local` |
| `IMAGE_TAG` | version or `latest` |
| `DOMAIN` | your client portal domain |
| `ADMIN_DOMAIN` | your admin panel domain |
| `ADMIN_BASE_URL` | `https://<admin-domain>` |
| `CLIENT_BASE_URL` | `https://<domain>` |
| `NUXT_PUBLIC_BASE_URL` | `https://<domain>` |
| `ALLOWED_HOSTS` | `<domain>;<admin-domain>;hostpanel-api;localhost` |
| `ACME_EMAIL` | your admin email |
| `SMTP_PORT` | `587` |
| `SMTP_ENCRYPTION` | `tls` |
| `SMTP_FROM` | your admin email |
| `SMTP_FROM_NAME` | your domain |
| `EMAIL_TO` | your admin email |

And these secrets, freshly generated with `openssl rand`:

| Secret | How generated |
|---|---|
| `JWT_SECRET` | `openssl rand -hex 32` |
| `ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `SESSION_ENCRYPTION_KEY` | `openssl rand -base64 32` |
| `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` | `openssl rand -hex 24` |
| `POSTGRES_PASSWORD` | `openssl rand -hex 24` |
| `RABBITMQ_PASSWORD` | `openssl rand -hex 24` |

After writing `.env` the installer pulls the images, starts the stack and waits up
to five minutes for `GET /health` to return `200`.

<Callout type="danger">
Keep `.env` and `ENCRYPTION_KEY` safe. Without the same key a restore cannot
read server passwords, API tokens or access hashes stored in the database. Back up
`.env` alongside every database dump. See [Back up and restore](/operators/backup).
</Callout>

## Install by hand with docker compose

If you prefer not to use the install script:

**1. Download the three files into an empty directory:**

```bash
BASE=https://raw.githubusercontent.com/innovayse/hostpanel/main
mkdir -p /opt/hostpanel && cd /opt/hostpanel
curl -fsSL "$BASE/docker/docker-compose.hub.yml" \
  -o docker-compose.hub.yml
curl -fsSL "$BASE/docker/caddy/Caddyfile" -o Caddyfile
curl -fsSL "$BASE/docker/.env.example" -o .env.example
```

**2. Copy `.env.example` to `.env` with restricted permissions:**

```bash
cp .env.example .env && chmod 600 .env
```

**3. Edit `.env`.** The example ships with `IMAGE_TAG=local`, `ALLOWED_HOSTS=panel.example.test`,
`POSTGRES_DB=innovayse_dev`, a placeholder `ENCRYPTION_KEY` and `SMTP_HOST=hostpanel-mailhog` —
leaving these unchanged will fail the pull or be refused on startup. Set at least:

| Setting | Example value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `IMAGE_TAG` | `latest` |
| `DOMAIN` | `panel.example.com` |
| `ADMIN_DOMAIN` | `admin.example.com` |
| `ADMIN_BASE_URL` | `https://admin.example.com` |
| `CLIENT_BASE_URL` | `https://panel.example.com` |
| `NUXT_PUBLIC_BASE_URL` | `https://panel.example.com` |
| `ALLOWED_HOSTS` | `panel.example.com;admin.example.com;hostpanel-api;localhost` |
| `ACME_EMAIL` | `you@example.com` |
| `EMAIL_TO` | `you@example.com` |
| `SMTP_HOST` | `smtp.example.com` |
| `SMTP_ENCRYPTION` | `tls` |
| `POSTGRES_DB` | `hostpanel` |

Generate the required secrets:

```bash
openssl rand -hex 32     # paste as JWT_SECRET
openssl rand -base64 32  # paste as ENCRYPTION_KEY
openssl rand -base64 32  # paste as SESSION_ENCRYPTION_KEY
openssl rand -hex 24     # paste as HOSTPANEL_BACKCHANNEL_LOGOUT_KEY
openssl rand -hex 24     # paste as POSTGRES_PASSWORD
openssl rand -hex 24     # paste as RABBITMQ_PASSWORD
```

See the [configuration reference](/operators/configuration) for all settings.

**4. Pull and start the stack:**

```bash
docker compose -f docker-compose.hub.yml pull
docker compose -f docker-compose.hub.yml up -d
```

## Set up mail

<Callout type="warning">
The installer leaves <code>SMTP_HOST</code> empty on purpose. Until you set a real relay,
no verification, password reset or invoice email is sent.
</Callout>

Edit `/opt/hostpanel/.env` and set:

| Setting | Description |
|---|---|
| `SMTP_HOST` | Your relay host, e.g. `smtp.example.com` |
| `SMTP_PORT` | Usually `587` (STARTTLS) or `465` (implicit TLS) |
| `SMTP_USERNAME` | Relay username |
| `SMTP_PASSWORD` | Relay password |
| `SMTP_ENCRYPTION` | `tls` (STARTTLS), `ssl` (implicit TLS) or `none` |
| `SMTP_FROM` | Sender address, e.g. `noreply@example.com` |
| `SMTP_FROM_NAME` | Sender display name, e.g. `Example Hosting` |

Then restart the stack to pick up the change:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml up -d
```

The API validates `SMTP_ENCRYPTION` on startup and names the key in the log if it is
wrong — see [Troubleshooting](#troubleshooting).

## First run

<Label type="local" /> <Label type="admin" />

When no one holds the Admin role yet, the admin panel shows a two-step setup
screen under the heading **Initial setup** instead of the sign-in page.

**Step 1 of 2 — create the first account on this installation**

Fill in **First name**, **Last name**, **Email address** and **Password**, then
click **Create account**.

**Step 2 of 2 — claim the administrator role**

You are now signed in. The screen shows "Signed in as `<your email>`." and a
**Setup token** field (placeholder: "Paste the token from the server log").

The installer printed the token at the end of its output. If you closed that
terminal, read it from the API log:

```bash
cd /opt/hostpanel
docker compose -f docker-compose.hub.yml logs hostpanel-api \
  | grep "setup token"
```

The line looks like this:

```
FIRST-RUN SETUP IS OUTSTANDING. Nobody holds the Admin role yet.
Open the admin panel, sign in, and paste this setup token when
it asks for one: <token>
```

<Callout type="tip">
Restarting the API prints the setup token again to its log as long as setup is
outstanding.
</Callout>

Paste the token and click **Claim the administrator role**. The admin dashboard
opens and the setup screen never appears again.

**Error messages on this screen:**

| Error | Meaning |
|---|---|
| "That setup token is not the one this installation issued..." | Wrong or stale token. Restart the API and read the newest one from the log. |
| "This installation has already been set up and someone already holds the administrator role." | Setup is done. Sign in normally, or ask the existing administrator for access. |
| "Invalid email or password." | Wrong credentials on the sign-in page after setup is complete. |

You can check whether setup is still outstanding without opening the admin panel:

```bash
curl https://admin.example.com/api/auth/setup-required
```

Returns `true` while the Admin role is unclaimed, `false` once setup is complete.

## After the install

Check that everything is working:

- Both the client portal and admin panel URLs open in a browser.
- You can sign in to the admin panel.
- `GET /health` returns `200`.

## Troubleshooting

**The API refuses to start**

The API validates secrets and settings on startup and logs the exact key that
failed. Read the log:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-api
```

Common causes:

| Symptom in the log | Cause |
|---|---|
| Secret still `CHANGE_ME` or too short | `JWT_SECRET`, `ENCRYPTION_KEY`, `SESSION_ENCRYPTION_KEY` or `HOSTPANEL_BACKCHANNEL_LOGOUT_KEY` holds a placeholder. Generate a real value with `openssl rand`. |
| `SMTP_ENCRYPTION` invalid | Value is unset, misspelled or not one of `tls`, `ssl`, `none`. |
| Only one of an optional pair is set | e.g. `TELEGRAM_BOT_TOKEN` set but `TELEGRAM_CHAT_ID` empty. Set both or clear both. |

**Certificates are not issued**

DNS for both hosts must resolve to this server before the first request. Check
with `dig panel.example.com` and re-run the installer once DNS is ready.

**The API did not become healthy in time**

The installer waits up to five minutes. If it times out, check the API log for
startup errors (see above) and the database log:

```bash
cd /opt/hostpanel && docker compose -f docker-compose.hub.yml logs hostpanel-db
```
