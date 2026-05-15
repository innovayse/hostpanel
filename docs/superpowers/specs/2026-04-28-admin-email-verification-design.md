# Admin Email Verification — Design Spec

## Overview

After the initial admin setup, the admin must verify their email before accessing dashboard functionality. The dashboard is navigable but shows a verification-required overlay on every section until verified.

## Backend

### New Endpoints

**`GET /api/auth/email-verified`** (Authenticated)
- Returns `{ verified: true/false }` based on `EmailConfirmed` field of the current JWT user.

**`GET /api/auth/verify-email?token=xxx&email=yyy`** (Anonymous)
- Validates the email confirmation token via `UserManager.ConfirmEmailAsync()`.
- Returns `{ success: true }` on success, 400 on invalid/expired token.

### Modified Endpoints

**`POST /api/auth/setup`**
- After creating the admin user, generate an email confirmation token via `UserManager.GenerateEmailConfirmationTokenAsync()`.
- Seed the `admin-email-verification` email template if it doesn't exist.
- Send the verification email via `SendEmailCommand` with the template slug `admin-email-verification`.
- The verification link points to `{baseUrl}/verify-email?token={urlEncodedToken}&email={urlEncodedEmail}` where `baseUrl` is the admin panel URL (from config or env var `ADMIN_BASE_URL`, default `http://localhost:5173`).

**`POST /api/auth/login`**
- No change to login itself. The `email-verified` endpoint is called separately by the frontend after login.

### IUserService Changes

Add to interface and implementation:
- `Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken ct)`
- `Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct)`
- `Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct)`

### Email Template

Slug: `admin-email-verification`

Seeded by the setup endpoint with:
- **Subject:** `Verify your email — Innovayse Admin`
- **Body (Liquid):**
```html
<h2>Welcome to Innovayse</h2>
<p>Click the link below to verify your email address and activate your admin account.</p>
<p><a href="{{ verification_link }}">Verify Email Address</a></p>
<p>If you did not create this account, ignore this email.</p>
```

Template data passed: `{ verification_link: "http://localhost:5173/verify-email?token=...&email=..." }`

### SMTP Configuration

Already configured in Docker Compose:
- Dev: MailHog on port 1025 (UI at localhost:8025)
- The `Smtp` section in appsettings.json needs `FromEmail` and `FromName` defaults added for dev.

## Admin Frontend

### New Route: `/verify-email`

**Page:** `VerifyEmailView.vue`
- Reads `token` and `email` from query params.
- On mount, calls `GET /api/auth/verify-email?token=...&email=...`.
- Shows loading spinner while verifying.
- On success: shows "Email successfully verified!" with a "Go to Admin Panel" button linking to `/login`.
- On failure: shows "Verification failed. The link may be expired or invalid."
- Route meta: `{ public: true }` (no auth required).

### Auth Store Changes

- New state: `emailVerified: ref<boolean | null>(null)` — null means not yet checked.
- New action: `checkEmailVerified()` — calls `GET /api/auth/email-verified`, sets the flag.
- Called after `login()` succeeds and after `fetchMe()` restores session.

### Verification Overlay

- A reusable component `EmailVerificationBanner.vue` shown inside `AppLayout.vue` when `emailVerified === false`.
- Renders over the page content area (not the sidebar).
- Message: "Please verify your email address to access the admin panel. Check your inbox for the verification link."
- Optional "Resend" button (calls `POST /api/auth/resend-verification` — stretch goal, not in v1).
- When `emailVerified` is `true` or `null` (still loading), the banner is hidden.

### Middleware Changes

No middleware changes needed. The setup-required check already works. The verification overlay is purely a UI concern inside `AppLayout.vue`.

## Not In Scope

- Resend verification email endpoint (can add later)
- Client portal email verification (separate concern)
- Token expiry customization (use Identity defaults)
- Password reset flow
