---
title: Authentication
description: "Sign in to the Hostpanel REST API and send the access token or session cookie. Covers two-factor sign-in, token lifetime and sign-on mode."
keywords: "hostpanel api authentication, bearer token, session cookie, two-factor sign-in, rest api"
---

# Authentication

A sign-in gives you two things at once: a session cookie for browsers, and an access token in the response body for everything else. Send either one on the calls that need it. Scripts and other servers use the token.

<Callout type="note">
There are no long-lived API tokens yet. Every token comes from a sign-in, lasts 15 minutes and is renewed only while you keep working. A script signs in again when its session reaches the maximum age.
</Callout>

## Sign in

<Label type="local" /> Local accounts only: under an external sign-on service these routes answer 404, see [Sign-on mode](#sign-on-mode).

```
POST /api/auth/login
```

Send the email address and the password. Answers `200` with the token, and sets the session cookie in the same response:

<SettingTable :settings="[
  { name: 'email', type: 'string', default: 'None', description: 'The email address of the account.' },
  { name: 'password', type: 'string', default: 'None', description: 'The password of the account.' },
]" />

The response is the token and how long it lives:

<SettingTable :settings="[
  { name: 'accessToken', type: 'string', default: 'None', description: 'The signed token. Send it in the Authorization header.' },
  { name: 'expiresIn', type: 'number', default: 'None', description: 'The lifetime of the token in seconds. 900, that is 15 minutes.' },
]" />

Wrong credentials answer `SIGN_IN_UNAUTHORIZED` (401). After too many wrong passwords in a row the account is locked and answers `SIGN_IN_ACCOUNT_LOCKED` (423), see [Rate limits](./rate-limits#sign-in-lockout).

## Bearer token

Send the token in the `Authorization` header:

```
Authorization: Bearer <token>
```

A token lasts 15 minutes. While you keep calling, the API hands you a fresh one: in the last third of its life, a successful response carries the new token in the `X-Renewed-Access-Token` header. Use it for your next calls. A session is never renewed past its maximum age, counted from the sign-in, and then you sign in again. An install sets that age with `SESSION_MAX_MINUTES`, 720 minutes by default.

<SettingTable :settings="[
  { name: 'Authorization', type: 'request header', default: 'None', description: 'Bearer followed by the access token. When it is present, the cookie is ignored.' },
  { name: 'X-Renewed-Access-Token', type: 'response header', default: 'None', description: 'A fresh access token, sent when the one you used is in the last third of its life and the call succeeded.' },
]" />

## Session cookie

The sign-in response also sets the session cookie, `hostpanel_session`. It is `HttpOnly`, so a script on the page cannot read it, and `SameSite=Strict`, so a browser never sends it from another site. It is `Secure` everywhere except on a local development host. A browser that holds it needs nothing else for its own calls. The API re-issues the cookie as it renews the session.

A request that changes state, that is any method except `GET`, `HEAD`, `OPTIONS` and `TRACE`, and that carries the session cookie must also carry this header, or it is refused with `CSRF_HEADER_MISSING` (403):

<SettingTable :settings="[
  { name: 'X-Requested-With', type: 'request header', default: 'XMLHttpRequest', description: 'Required on a request that changes state when the session cookie is the credential. A page on another site cannot add this header without a cross-origin permission this API does not give.' },
]" />

A request that authenticates with a bearer token does not need it. The plugin routes, `/api/admin/plugins/{id}/ext/...` and `/api/client/plugins/{id}/...`, ask for the header or a bearer token on every request that changes state.

## Sign out

```
DELETE /api/auth/logout
```

Clears the session cookie and answers `200`, whether or not there was a session. A token is stateless: there is nothing to revoke on the server, so a copy of one stays valid until it expires. A password reset, switching two-factor off and a change of email address end every session of the account at once.

## Two-factor authentication

When the account has two-factor on, signing in takes two calls. The first answers `200` with a pending token instead of an access token, and sets no cookie:

```json
{
  "twoFactorRequired": true,
  "pendingToken": "..."
}
```

Send the pending token with the code from the authenticator app:

```
POST /api/auth/2fa-login
```

<SettingTable :settings="[
  { name: 'pendingToken', type: 'string', default: 'None', description: 'The pending token from the first call. It is short-lived.' },
  { name: 'code', type: 'string', default: 'None', description: 'The code from the authenticator app.' },
]" />

On success the answer is the same as a sign-in without two-factor: the access token and the session cookie. A wrong code answers `TWO_FACTOR_CODE_UNAUTHORIZED` (401). A pending token that is malformed, expired or already used answers `PENDING_TOKEN_UNAUTHORIZED` (401).

## What each audience needs

Every endpoint page states its requirement under **Access**. The requirements are these:

<SettingTable :settings="[
  { name: 'Anonymous', type: '', default: 'None', description: 'No credential. The sign-in routes and the public storefront routes.' },
  { name: 'Any signed-in user', type: '', default: 'None', description: 'A valid session, of a client or of staff.' },
  { name: 'Client', type: '', default: 'None', description: 'A signed-in client user acting on their own account. A staff sign-in has no client account and is refused.' },
  { name: 'Client permission', type: '', default: 'None', description: 'Some client routes also need a permission that the account owner gave the user, such as ViewPayInvoices. A user without it gets CLIENT_PERMISSION_DENIED (403).' },
  { name: 'Admin', type: '', default: 'None', description: 'A signed-in member of staff with the Admin role.' },
]" />

The permissions a client user can hold are listed under [Users and permissions](../customers/account#permissions) in the customer guide.

A call without a credential, or with an expired one, answers `UNAUTHORIZED` (401). A call with a credential that is not allowed there answers `FORBIDDEN` (403).

## Sign-on mode

<Label type="sso" /> An install can hand sign-in to an external sign-on service by setting `AUTH_MODE=sso`. In that mode the local routes of the API, `login`, `2fa-login`, `register`, `forgot-password`, `reset-password` and `confirm-email`, answer 404, and the API issues no tokens of its own: sessions and tokens come from the sign-on service. `GET /api/auth/mode` tells a client which mode the install is in. See [Sign-in modes](../operators/configuration#sign-in-modes) for the settings.

## A full example

These three calls sign in, call a client route and call an admin route. A client route needs a client user's sign-in, and an admin route needs a member of staff, so the example signs in twice.

**Step 1. Sign in as a client user and read the token.**

::: code-group

```bash [curl]
curl -X POST 'https://panel.example.com/api/auth/login' \
  -H 'Content-Type: application/json' \
  -d '{"email": "client@example.com", "password": "your-password"}'
```

```js [JavaScript]
const signIn = await fetch('https://panel.example.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'client@example.com', password: 'your-password' }),
})
const { accessToken } = await signIn.json()
```

:::

The answer carries the token:

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 900
}
```

**Step 2. Call a client route with the token.** This lists the invoices of the signed-in client.

::: code-group

```bash [curl]
curl 'https://panel.example.com/api/me/invoices' \
  -H 'Authorization: Bearer <token>'
```

```js [JavaScript]
const invoices = await fetch('https://panel.example.com/api/me/invoices', {
  headers: { Authorization: `Bearer ${accessToken}` },
})
console.log(await invoices.json())
```

:::

**Step 3. Call an admin route.** Sign in as a member of staff the same way, then read a page of clients with that token:

::: code-group

```bash [curl]
curl 'https://admin.example.com/api/clients?Page=1&PageSize=20' \
  -H 'Authorization: Bearer <staff-token>'
```

```js [JavaScript]
const clients = await fetch('https://admin.example.com/api/clients?Page=1&PageSize=20', {
  headers: { Authorization: `Bearer ${staffToken}` },
})
console.log(await clients.json())
```

:::

If a response carries `X-Renewed-Access-Token`, send that token on your next calls. The answer to the third call is a page of results, see [Pagination](./pagination).
