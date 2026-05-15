# Admin Email Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require the initial admin to verify their email before accessing dashboard functionality, using the existing email template system and MailHog for dev testing.

**Architecture:** Add 3 new API endpoints (verify-email, email-verified, setup modification). The admin frontend gets a verification page and a banner overlay inside AppLayout. Uses ASP.NET Identity's built-in email confirmation tokens and the existing SendEmailCommand/MailKit pipeline.

**Tech Stack:** ASP.NET Core Identity (token providers), Wolverine (SendEmailCommand), Liquid templates, MailKit/MailHog, Vue 3 + Pinia

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `backend/src/Innovayse.Application/Auth/Interfaces/IUserService.cs` | Modify | Add 3 new methods |
| `backend/src/Innovayse.Infrastructure/Auth/UserService.cs` | Modify | Implement 3 new methods |
| `backend/src/Innovayse.API/Auth/AuthController.cs` | Modify | Add verify-email + email-verified endpoints, modify setup |
| `backend/src/Innovayse.API/appsettings.json` | Modify | Add Smtp section with dev defaults |
| `docker-compose.yml` | Modify | Add SMTP env vars for API |
| `.env.example` | Modify | Add ADMIN_BASE_URL |
| `admin/src/modules/auth/stores/authStore.ts` | Modify | Add emailVerified state + checkEmailVerified action |
| `admin/src/modules/auth/views/VerifyEmailView.vue` | Create | Email verification landing page |
| `admin/src/components/layout/EmailVerificationBanner.vue` | Create | Overlay banner for unverified users |
| `admin/src/components/layout/AppLayout.vue` | Modify | Include verification banner |
| `admin/src/router/index.ts` | Modify | Add /verify-email route |

---

### Task 1: Add IUserService methods and implementation

**Files:**
- Modify: `backend/src/Innovayse.Application/Auth/Interfaces/IUserService.cs`
- Modify: `backend/src/Innovayse.Infrastructure/Auth/UserService.cs`

- [ ] **Step 1: Add 3 new methods to IUserService**

Add these methods to the `IUserService` interface, before the closing brace:

```csharp
/// <summary>
/// Generates an email confirmation token for the specified user.
/// </summary>
/// <param name="userId">The user's unique identifier.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The generated email confirmation token string.</returns>
Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken ct);

/// <summary>
/// Confirms the user's email address using the provided token.
/// </summary>
/// <param name="email">The user's email address.</param>
/// <param name="token">The email confirmation token.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>True if confirmation succeeded; false otherwise.</returns>
Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct);

/// <summary>
/// Checks whether the specified user's email has been confirmed.
/// </summary>
/// <param name="userId">The user's unique identifier.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>True if the user's email is confirmed; false otherwise.</returns>
Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct);
```

- [ ] **Step 2: Implement the 3 methods in UserService**

Add these methods to `UserService`, before the closing brace:

```csharp
/// <inheritdoc/>
public async Task<string> GenerateEmailConfirmationTokenAsync(string userId, CancellationToken ct)
{
    var user = await userManager.FindByIdAsync(userId)
        ?? throw new InvalidOperationException($"User {userId} not found.");
    return await userManager.GenerateEmailConfirmationTokenAsync(user);
}

/// <inheritdoc/>
public async Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct)
{
    var user = await userManager.FindByEmailAsync(email);
    if (user is null) return false;
    var result = await userManager.ConfirmEmailAsync(user, token);
    return result.Succeeded;
}

/// <inheritdoc/>
public async Task<bool> IsEmailConfirmedAsync(string userId, CancellationToken ct)
{
    var user = await userManager.FindByIdAsync(userId);
    return user?.EmailConfirmed ?? false;
}
```

- [ ] **Step 3: Commit**

```bash
git add backend/src/Innovayse.Application/Auth/Interfaces/IUserService.cs backend/src/Innovayse.Infrastructure/Auth/UserService.cs
git commit -m "feat: add email confirmation methods to IUserService"
```

---

### Task 2: Add SMTP config defaults and env vars

**Files:**
- Modify: `backend/src/Innovayse.API/appsettings.json`
- Modify: `docker-compose.yml`
- Modify: `.env.example`

- [ ] **Step 1: Add Smtp section to appsettings.json**

Add the following after the `"Cors"` section (before the final closing brace):

```json
,
"Smtp": {
  "Host": "localhost",
  "Port": 1025,
  "Username": "",
  "Password": "",
  "FromEmail": "noreply@innovayse.com",
  "FromName": "Innovayse",
  "UseSsl": false
},
"AdminBaseUrl": "http://localhost:5173"
```

- [ ] **Step 2: Add SMTP and ADMIN_BASE_URL env vars to docker-compose.yml API service**

In the `api` service `environment` block in `docker-compose.yml`, add these lines after the existing `Cors__AllowedOrigins__1` line:

```yaml
      Smtp__Host: mailhog
      Smtp__Port: 1025
      Smtp__Username: ""
      Smtp__Password: ""
      Smtp__FromEmail: noreply@innovayse.com
      Smtp__FromName: Innovayse
      Smtp__UseSsl: "false"
      AdminBaseUrl: ${ADMIN_BASE_URL:-http://localhost:5173}
```

- [ ] **Step 3: Add ADMIN_BASE_URL to .env.example**

Add this line after the `CORS_ORIGINS` line:

```env
# Admin panel URL (used for verification email links)
ADMIN_BASE_URL=http://localhost:5173
```

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.API/appsettings.json docker-compose.yml .env.example
git commit -m "chore: add SMTP defaults and ADMIN_BASE_URL config"
```

---

### Task 3: Add verify-email and email-verified endpoints, modify setup

**Files:**
- Modify: `backend/src/Innovayse.API/Auth/AuthController.cs`

- [ ] **Step 1: Add Wolverine IMessageBus using and IConfiguration to controller constructor**

Replace the controller class declaration and constructor to add `IConfiguration`:

```csharp
/// <param name="bus">Wolverine message bus for dispatching commands.</param>
/// <param name="userService">User management service for setup checks.</param>
/// <param name="configuration">Application configuration for reading settings.</param>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMessageBus bus, IUserService userService, IConfiguration configuration) : ControllerBase
{
```

Add this using at the top of the file (with the existing usings):

```csharp
using Innovayse.Application.Notifications.Commands.SendEmail;
```

- [ ] **Step 2: Modify the SetupAsync endpoint to send verification email**

Replace the entire `SetupAsync` method with:

```csharp
/// <summary>
/// Creates the initial admin account. Only works when no users exist.
/// Sends a verification email to the admin's address.
/// </summary>
/// <param name="request">Registration data for the admin user.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Access token and expiry for the new admin.</returns>
[HttpPost("setup")]
[AllowAnonymous]
[ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> SetupAsync([FromBody] RegisterRequest request, CancellationToken ct)
{
    var anyUsers = await userService.AnyUsersExistAsync(ct);
    if (anyUsers)
    {
        return Conflict(new { error = "Setup already completed. An admin account exists." });
    }

    var userId = await userService.CreateAsync(request.Email, request.Password, ct);
    await userService.AddToRoleAsync(userId, Roles.Admin, ct);

    var tokenService = HttpContext.RequestServices.GetRequiredService<ITokenService>();
    var refreshTokenRepo = HttpContext.RequestServices.GetRequiredService<IRefreshTokenRepository>();
    var uow = HttpContext.RequestServices.GetRequiredService<Application.Common.IUnitOfWork>();

    // Seed the email verification template if it doesn't exist
    var templateRepo = HttpContext.RequestServices.GetRequiredService<Domain.Notifications.Interfaces.IEmailTemplateRepository>();
    var existingTemplate = await templateRepo.FindBySlugAsync("admin-email-verification", ct);
    if (existingTemplate is null)
    {
        var template = Domain.Notifications.EmailTemplate.Create(
            "admin-email-verification",
            "Verify your email — Innovayse Admin",
            "<h2>Welcome to Innovayse</h2><p>Click the link below to verify your email address and activate your admin account.</p><p><a href=\"{{ verification_link }}\">Verify Email Address</a></p><p>If you did not create this account, ignore this email.</p>",
            "Sent to new admin users to verify their email address.");
        templateRepo.Add(template);
        await uow.SaveChangesAsync(ct);
    }

    // Generate email confirmation token and send verification email
    var confirmationToken = await userService.GenerateEmailConfirmationTokenAsync(userId, ct);
    var adminBaseUrl = configuration["AdminBaseUrl"] ?? "http://localhost:5173";
    var verificationLink = $"{adminBaseUrl}/verify-email?token={Uri.EscapeDataString(confirmationToken)}&email={Uri.EscapeDataString(request.Email)}";

    await bus.InvokeAsync(new SendEmailCommand(
        request.Email,
        "admin-email-verification",
        new { verification_link = verificationLink }), ct);

    // Issue auth tokens
    var (accessToken, expiresAt) = tokenService.GenerateAccessToken(userId, request.Email, Roles.Admin);
    var (refreshToken, refreshExpiresAt) = tokenService.GenerateRefreshToken(userId);

    await refreshTokenRepo.AddAsync(userId, refreshToken, refreshExpiresAt, ct);
    await uow.SaveChangesAsync(ct);

    SetRefreshTokenCookie(refreshToken);
    return Ok(new AuthResultDto(accessToken, expiresAt, Roles.Admin));
}
```

- [ ] **Step 3: Add the verify-email endpoint**

Add this method after the `SetupAsync` method:

```csharp
/// <summary>
/// Verifies an admin's email address using a confirmation token.
/// </summary>
/// <param name="token">The email confirmation token.</param>
/// <param name="email">The user's email address.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Success or failure result.</returns>
[HttpGet("verify-email")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> VerifyEmailAsync(
    [FromQuery] string token,
    [FromQuery] string email,
    CancellationToken ct)
{
    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
    {
        return BadRequest(new { error = "Token and email are required." });
    }

    var success = await userService.ConfirmEmailAsync(email, token, ct);
    if (!success)
    {
        return BadRequest(new { error = "Invalid or expired verification token." });
    }

    return Ok(new { success = true });
}
```

- [ ] **Step 4: Add the email-verified endpoint**

Add this method after the `VerifyEmailAsync` method:

```csharp
/// <summary>
/// Returns whether the current user's email has been verified.
/// </summary>
/// <param name="ct">Cancellation token.</param>
/// <returns>Object with <c>verified</c> boolean.</returns>
[HttpGet("email-verified")]
[Authorize]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> EmailVerifiedAsync(CancellationToken ct)
{
    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized();
    }

    var verified = await userService.IsEmailConfirmedAsync(userId, ct);
    return Ok(new { verified });
}
```

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.API/Auth/AuthController.cs
git commit -m "feat: add email verification endpoints and send verification on setup"
```

---

### Task 4: Update admin authStore with emailVerified state

**Files:**
- Modify: `admin/src/modules/auth/stores/authStore.ts`

- [ ] **Step 1: Replace the entire authStore.ts**

```typescript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useApi, setToken, clearToken } from '../../../composables/useApi'

/**
 * Decodes the payload of a JWT token without verifying the signature.
 *
 * @param token - JWT string with three base64url segments.
 * @returns Parsed payload object or null if decoding fails.
 */
function decodeJwt(token: string): Record<string, unknown> | null {
  try {
    const payload = token.split('.')[1] ?? ''
    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    return JSON.parse(json)
  } catch {
    return null
  }
}

/**
 * Extracts email and role from a JWT payload.
 *
 * @param token - JWT access token string.
 * @returns User object with email and role, or null.
 */
function userFromToken(token: string): { email: string; role: string } | null {
  const payload = decodeJwt(token)
  if (!payload) return null
  const email = payload['email'] as string | undefined
  // ASP.NET Identity writes role as the long-form claim URI
  const role = (
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
    payload['role']
  ) as string | undefined
  if (!email || !role) return null
  return { email, role }
}

/**
 * Pinia store managing admin authentication state.
 *
 * Handles login, logout, session restoration, and email verification status.
 * Decodes user info from the token — no extra API call needed.
 */
export const useAuthStore = defineStore('auth', () => {
  /** Currently authenticated admin user, null when unauthenticated. */
  const user = ref<{ email: string; role: string } | null>(null)

  /** Whether the current user's email has been verified. Null means not yet checked. */
  const emailVerified = ref<boolean | null>(null)

  const { request } = useApi()

  /** True when a user session is active. */
  const isAuthenticated = computed(() => user.value !== null)

  /**
   * Logs in with email and password credentials.
   *
   * Stores the returned JWT, decodes user info, and checks email verification status.
   *
   * @param email - Admin email address.
   * @param password - Admin password.
   * @returns Promise that resolves after login completes.
   */
  async function login(email: string, password: string): Promise<void> {
    const data = await request<{ accessToken: string; role: string }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    setToken(data.accessToken)
    user.value = userFromToken(data.accessToken)
    await checkEmailVerified()
  }

  /**
   * Checks whether the current user's email has been verified.
   *
   * @returns Promise that resolves when the check completes.
   */
  async function checkEmailVerified(): Promise<void> {
    try {
      const data = await request<{ verified: boolean }>('/auth/email-verified')
      emailVerified.value = data.verified
    } catch {
      emailVerified.value = null
    }
  }

  /**
   * Restores session from a stored JWT token (page refresh).
   *
   * Sets user to null if no valid token is found.
   * Also checks email verification status if session is valid.
   *
   * @returns Promise that resolves when session is restored.
   */
  async function fetchMe(): Promise<void> {
    const stored = localStorage.getItem('admin_token')
    if (!stored) {
      user.value = null
      emailVerified.value = null
      return
    }
    const parsed = userFromToken(stored)
    if (!parsed) {
      clearToken()
      user.value = null
      emailVerified.value = null
      return
    }
    user.value = parsed
    await checkEmailVerified()
  }

  /**
   * Logs out the current user and clears session state.
   *
   * @returns Promise that resolves after logout completes.
   */
  async function logout(): Promise<void> {
    try {
      await request('/auth/logout', { method: 'POST' })
    } finally {
      clearToken()
      user.value = null
      emailVerified.value = null
    }
  }

  return { user, emailVerified, isAuthenticated, login, logout, fetchMe, checkEmailVerified }
})
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/auth/stores/authStore.ts
git commit -m "feat: add emailVerified state to admin auth store"
```

---

### Task 5: Create VerifyEmailView page

**Files:**
- Create: `admin/src/modules/auth/views/VerifyEmailView.vue`
- Modify: `admin/src/router/index.ts`

- [ ] **Step 1: Create VerifyEmailView.vue**

```vue
<script setup lang="ts">
/**
 * Email verification landing page.
 *
 * Receives token and email from URL query params, calls the verify API,
 * and shows success or failure message.
 */
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useApi } from '../../../composables/useApi'

const route = useRoute()
const { request } = useApi()

/** True while the verification request is in flight. */
const loading = ref(true)

/** True when verification succeeded. */
const success = ref(false)

/** Error message shown when verification fails. */
const error = ref<string | null>(null)

onMounted(async () => {
  const token = route.query.token as string | undefined
  const email = route.query.email as string | undefined

  if (!token || !email) {
    error.value = 'Invalid verification link. Missing token or email.'
    loading.value = false
    return
  }

  try {
    await request<{ success: boolean }>(
      `/auth/verify-email?token=${encodeURIComponent(token)}&email=${encodeURIComponent(email)}`,
    )
    success.value = true
  } catch {
    error.value = 'Verification failed. The link may be expired or invalid.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="relative min-h-screen bg-surface-base flex items-center justify-center overflow-hidden">

    <!-- Orb blue (top-left) -->
    <div
      class="pointer-events-none absolute -top-32 -left-24 w-[480px] h-[480px] rounded-full opacity-20 blur-[90px]"
      style="background: radial-gradient(circle, #0ea5e9 0%, transparent 70%); animation: drift-a 12s ease-in-out infinite alternate;"
    />
    <!-- Orb purple (bottom-right) -->
    <div
      class="pointer-events-none absolute -bottom-24 -right-20 w-[420px] h-[420px] rounded-full opacity-20 blur-[90px]"
      style="background: radial-gradient(circle, #8b5cf6 0%, transparent 70%); animation: drift-a 15s ease-in-out infinite alternate-reverse;"
    />

    <!-- Card -->
    <div
      class="relative z-10 w-full max-w-sm mx-4 bg-surface-card/85 backdrop-blur-2xl border border-white/[0.06] rounded-2xl p-10 shadow-2xl text-center"
      style="animation: card-in 0.5s cubic-bezier(0.16,1,0.3,1) both;"
    >
      <!-- Loading -->
      <div v-if="loading" class="flex flex-col items-center gap-4">
        <div class="w-12 h-12 border-3 border-primary-500/30 border-t-primary-500 rounded-full animate-spin" />
        <p class="text-sm text-text-secondary">Verifying your email...</p>
      </div>

      <!-- Success -->
      <div v-else-if="success" class="flex flex-col items-center gap-5">
        <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2">
            <path d="M20 6L9 17l-5-5" />
          </svg>
        </div>
        <div>
          <h1 class="font-display text-xl font-bold text-text-primary mb-1.5">Email Verified!</h1>
          <p class="text-sm text-text-secondary">Your email has been successfully verified. You now have full access to the admin panel.</p>
        </div>
        <router-link
          to="/login"
          class="w-full py-3 rounded-[10px] font-display font-semibold text-[0.95rem] text-white gradient-brand border-none cursor-pointer transition-all duration-200 hover:-translate-y-px inline-block text-center"
          style="box-shadow: 0 4px 20px rgba(14,165,233,0.25);"
        >
          Go to Admin Panel
        </router-link>
      </div>

      <!-- Error -->
      <div v-else class="flex flex-col items-center gap-5">
        <div class="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2">
            <circle cx="12" cy="12" r="10" />
            <line x1="15" y1="9" x2="9" y2="15" />
            <line x1="9" y1="9" x2="15" y2="15" />
          </svg>
        </div>
        <div>
          <h1 class="font-display text-xl font-bold text-text-primary mb-1.5">Verification Failed</h1>
          <p class="text-sm text-text-secondary">{{ error }}</p>
        </div>
        <router-link
          to="/login"
          class="text-sm text-primary-400 hover:text-primary-300 transition-colors"
        >
          Back to login
        </router-link>
      </div>
    </div>
  </div>
</template>

<style>
@keyframes drift-a {
  from { transform: translate(0, 0); }
  to   { transform: translate(40px, 30px); }
}
@keyframes card-in {
  from { opacity: 0; transform: translateY(18px) scale(0.98); }
  to   { opacity: 1; transform: translateY(0) scale(1); }
}
</style>
```

- [ ] **Step 2: Add the /verify-email route to the router**

In `admin/src/router/index.ts`, add this route after the `/setup` route:

```typescript
    {
      path: '/verify-email',
      component: () => import('../modules/auth/views/VerifyEmailView.vue'),
      meta: { public: true },
    },
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/modules/auth/views/VerifyEmailView.vue admin/src/router/index.ts
git commit -m "feat: add email verification landing page"
```

---

### Task 6: Create EmailVerificationBanner and integrate into AppLayout

**Files:**
- Create: `admin/src/components/layout/EmailVerificationBanner.vue`
- Modify: `admin/src/components/layout/AppLayout.vue`

- [ ] **Step 1: Create EmailVerificationBanner.vue**

```vue
<script setup lang="ts">
/**
 * Banner overlay shown when the current admin's email is not yet verified.
 *
 * Displays over page content (not the sidebar) with a message to check inbox.
 * Automatically hides when emailVerified becomes true.
 */
import { useAuthStore } from '../../modules/auth/stores/authStore'

const authStore = useAuthStore()
</script>

<template>
  <div
    v-if="authStore.emailVerified === false"
    class="absolute inset-0 z-20 flex items-center justify-center bg-surface-base/80 backdrop-blur-sm"
  >
    <div class="max-w-md mx-4 bg-surface-card border border-white/[0.06] rounded-2xl p-8 shadow-2xl text-center">
      <div class="w-14 h-14 mx-auto mb-5 rounded-full bg-amber-500/10 border border-amber-500/20 flex items-center justify-center">
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="1.5">
          <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
          <polyline points="22,6 12,13 2,6"/>
        </svg>
      </div>
      <h2 class="font-display text-lg font-bold text-text-primary mb-2">Verify Your Email</h2>
      <p class="text-sm text-text-secondary mb-4">
        Please check your inbox and click the verification link to activate your admin account. You won't be able to use the admin panel until your email is verified.
      </p>
      <p class="text-xs text-text-muted">
        Didn't receive the email? Check your spam folder.
      </p>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Integrate the banner into AppLayout.vue**

Replace the entire `AppLayout.vue` with:

```vue
<script setup lang="ts">
/**
 * Main authenticated layout — sidebar + topbar + content area.
 *
 * Shows email verification banner over content when admin email is unverified.
 *
 * Handles responsive sidebar state:
 * - Desktop (lg+): always visible, full width
 * - Tablet (md): icon-only collapsed sidebar
 * - Mobile (<md): hidden drawer, toggled via topbar hamburger
 */
import { ref } from 'vue'
import AppSidebar from './AppSidebar.vue'
import AppTopbar from './AppTopbar.vue'
import EmailVerificationBanner from './EmailVerificationBanner.vue'
import { RouterView } from 'vue-router'

/** Controls the mobile drawer open state. */
const drawerOpen = ref(false)

/** Toggles the mobile sidebar drawer. */
function toggleSidebar(): void {
  drawerOpen.value = !drawerOpen.value
}

/** Closes the mobile drawer (called on nav item click or backdrop). */
function closeDrawer(): void {
  drawerOpen.value = false
}
</script>

<template>
  <div class="flex min-h-screen bg-surface-base">

    <!-- Mobile backdrop -->
    <Transition name="fade">
      <div
        v-if="drawerOpen"
        class="fixed inset-0 z-30 bg-black/60 backdrop-blur-sm lg:hidden"
        @click="closeDrawer"
      />
    </Transition>

    <!-- Sidebar -->
    <!-- Desktop: always visible | Mobile: fixed drawer -->
    <div
      class="fixed inset-y-0 left-0 z-40 lg:static lg:z-auto lg:flex transition-transform duration-300 ease-in-out"
      :class="drawerOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'"
    >
      <AppSidebar @navigate="closeDrawer" />
    </div>

    <!-- Main column: topbar + content -->
    <div class="flex flex-col flex-1 min-w-0 lg:ml-0">
      <AppTopbar @toggle-sidebar="toggleSidebar" />

      <main class="relative flex-1 overflow-auto">
        <RouterView />
        <EmailVerificationBanner />
      </main>
    </div>

  </div>
</template>

<style>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/components/layout/EmailVerificationBanner.vue admin/src/components/layout/AppLayout.vue
git commit -m "feat: add email verification banner overlay in admin layout"
```

---

### Task 7: Smoke Test

- [ ] **Step 1: Delete existing users to start fresh**

```bash
docker compose exec postgres psql -U postgres -d innovayse_dev -c "DELETE FROM clients; DELETE FROM \"AspNetUserRoles\"; DELETE FROM \"AspNetUsers\";"
```

- [ ] **Step 2: Rebuild and restart the API**

```bash
docker compose up -d --build api
```

Wait ~15 seconds for the API to start.

- [ ] **Step 3: Verify the setup-required endpoint**

```bash
curl -s http://localhost:5148/api/auth/setup-required
```

Expected: `{"required":true}`

- [ ] **Step 4: Open admin panel and complete setup**

Open http://localhost:5173 in an incognito/fresh tab.
- Should redirect to /setup.
- Fill in name, email, password.
- Click "Create Admin Account".
- Should redirect to /dashboard with the verification banner overlay.

- [ ] **Step 5: Check MailHog for the verification email**

Open http://localhost:8025 (MailHog UI).
- Should see the verification email with subject "Verify your email — Innovayse Admin".
- Click the verification link in the email.

- [ ] **Step 6: Verify the email**

The link should open the /verify-email page in the admin panel.
- Should show "Email Verified!" with a "Go to Admin Panel" button.
- Click the button to go to /login.

- [ ] **Step 7: Login and verify banner is gone**

Login with the admin credentials.
- Dashboard should load without the verification banner.

- [ ] **Step 8: Commit all remaining changes**

```bash
git add -A
git commit -m "feat: complete admin email verification flow"
```
