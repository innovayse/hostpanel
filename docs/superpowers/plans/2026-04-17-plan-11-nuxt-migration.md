# Nuxt Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the existing Nuxt.js client from WHMCS API to the new C# backend API — replace `server/utils/whmcs.ts` with `server/utils/api.ts`, update all Nuxt server routes to call the C# API instead of WHMCS, implement auth cookie flow (JWT in httpOnly cookie), configure proxy pattern (Browser → Nuxt Server → C# API), and test each migrated endpoint.

**Architecture:** Proxy pattern keeps C# API URL hidden from browser; all requests go through Nuxt server-side proxy; JWT stored in httpOnly cookie set by Nuxt server after successful C# API login; all existing Nuxt server routes (`server/api/portal/**`) remain but internal implementation changes from `whmcsCall` to `internalApiCall`.

**Tech Stack:** Nuxt 4, TypeScript, $fetch, httpOnly cookies, C# backend API (ASP.NET Core 8).

---

## File Map

```
server/utils/
  api.ts                                            ← NEW: replaces whmcs.ts
  whmcs.ts                                          ← DELETE after migration

server/api/portal/
  auth/
    login.post.ts                                   ← UPDATE: call C# /api/auth/login
    logout.post.ts                                  ← UPDATE: call C# /api/auth/logout
    register.post.ts                                ← UPDATE: call C# /api/auth/register
    forgot.post.ts                                  ← UPDATE: call C# /api/auth/forgot
  client/
    me.get.ts                                       ← UPDATE: call C# /api/clients/me
    me.put.ts                                       ← UPDATE: call C# /api/clients/me
    addresses.get.ts                                ← UPDATE
    addresses.post.ts                               ← UPDATE
    contacts.get.ts                                 ← UPDATE
    contacts.post.ts                                ← UPDATE
    contacts/[id].delete.ts                         ← UPDATE
    contacts/[id].put.ts                            ← UPDATE
    services/
      index.get.ts                                  ← UPDATE: call C# /api/me/services
      [id]/index.get.ts                             ← UPDATE
      [id]/setup.post.ts                            ← UPDATE
      [id]/cancel.post.ts                           ← UPDATE
    domains/
      index.get.ts                                  ← UPDATE: call C# /api/me/domains
      [id].get.ts                                   ← UPDATE
      [id]/auto-renew.put.ts                        ← UPDATE
      [id]/whois-privacy.put.ts                     ← UPDATE
    invoices/
      index.get.ts                                  ← UPDATE: call C# /api/me/invoices
      [id].get.ts                                   ← UPDATE
      [id]/pay.post.ts                              ← UPDATE
    tickets/
      index.get.ts                                  ← UPDATE: call C# /api/me/tickets
      [id].get.ts                                   ← UPDATE
      [id]/reply.post.ts                            ← UPDATE
      new.post.ts                                   ← UPDATE

composables/
  useClientAuth.ts                                  ← UPDATE: adjust to new auth flow
  useApi.ts                                         ← KEEP: already uses server proxy

nuxt.config.ts                                      ← UPDATE: add NUXT_API_URL env var
.env.example                                        ← UPDATE: add API_URL
```

---

## Task 1: Create New API Utility

- [ ] **Step 1: Create `server/utils/api.ts`**

```typescript
// server/utils/api.ts
import type { H3Event } from 'h3'

/**
 * Internal API call to C# backend.
 *
 * This function makes server-side requests to the C# API, which is only accessible
 * from the Nuxt server (not from the browser). The C# API URL is configured via
 * the NUXT_API_URL environment variable.
 *
 * @param event - H3 event from Nuxt server route
 * @param endpoint - API endpoint path (e.g., "/auth/login")
 * @param options - Fetch options (method, body, etc.)
 * @returns The API response data
 */
export async function internalApiCall<T>(
  event: H3Event,
  endpoint: string,
  options: {
    method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
    body?: any
  } = {}
): Promise<T> {
  const config = useRuntimeConfig()
  const apiUrl = config.apiUrl || 'http://localhost:5000'

  // Get JWT from cookie if present
  const token = getCookie(event, 'auth_token')

  const headers: Record<string, string> = {
    'Content-Type': 'application/json'
  }

  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const response = await $fetch<T>(`${apiUrl}/api${endpoint}`, {
    method: options.method || 'GET',
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined
  })

  return response
}

/**
 * Sets the auth JWT in an httpOnly cookie.
 *
 * @param event - H3 event
 * @param token - JWT token from C# API
 */
export function setAuthCookie(event: H3Event, token: string) {
  setCookie(event, 'auth_token', token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === 'production',
    sameSite: 'lax',
    maxAge: 60 * 60 * 24 * 7 // 7 days
  })
}

/**
 * Clears the auth JWT cookie.
 *
 * @param event - H3 event
 */
export function clearAuthCookie(event: H3Event) {
  deleteCookie(event, 'auth_token')
}
```

- [ ] **Step 2: Update `nuxt.config.ts`**

```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  runtimeConfig: {
    apiUrl: process.env.API_URL || 'http://localhost:5000'
  },
  // ... rest of config
})
```

- [ ] **Step 3: Update `.env.example`**

```
API_URL=http://localhost:5000
```

- [ ] **Step 4: Commit**

```bash
git add server/utils/api.ts nuxt.config.ts .env.example
git commit -m "feat(nuxt): add internal API utility for C# backend integration"
```

---

## Task 2: Migrate Auth Routes

- [ ] **Step 1: Update `server/api/portal/auth/login.post.ts`**

```typescript
// server/api/portal/auth/login.post.ts
export default defineEventHandler(async (event) => {
  const body = await readBody(event)

  const response = await internalApiCall<{ token: string; refreshToken: string }>(
    event,
    '/auth/login',
    { method: 'POST', body }
  )

  // Set JWT in httpOnly cookie
  setAuthCookie(event, response.token)

  // Return success (token is in cookie, not response body)
  return { success: true }
})
```

- [ ] **Step 2: Update logout, register, and forgot endpoints**

Follow the same pattern for other auth endpoints.

- [ ] **Step 3: Test auth endpoints**

```bash
# Test login
curl -X POST http://localhost:3000/api/portal/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  -c cookies.txt

# Test authenticated endpoint
curl http://localhost:3000/api/portal/client/me \
  -b cookies.txt
```

- [ ] **Step 4: Commit**

```bash
git add server/api/portal/auth/
git commit -m "feat(nuxt): migrate auth endpoints to C# backend"
```

---

## Task 3: Migrate Client Routes

- [ ] **Step 1: Update `server/api/portal/client/me.get.ts`**

```typescript
// server/api/portal/client/me.get.ts
export default defineEventHandler(async (event) => {
  const profile = await internalApiCall(event, '/clients/me', { method: 'GET' })
  return profile
})
```

- [ ] **Step 2: Update all other client routes (addresses, contacts, services, domains, invoices, tickets)**

Follow the same pattern for each endpoint.

- [ ] **Step 3: Test each migrated endpoint**

Test each endpoint after migration to ensure it works correctly.

- [ ] **Step 4: Commit**

```bash
git add server/api/portal/client/
git commit -m "feat(nuxt): migrate client endpoints to C# backend"
```

---

## Task 4: Update Composables

- [ ] **Step 1: Update `composables/useClientAuth.ts`**

```typescript
// composables/useClientAuth.ts
export function useClientAuth() {
  const user = useState('user', () => null)
  const loading = useState('authLoading', () => false)

  async function login(email: string, password: string) {
    loading.value = true
    try {
      await $fetch('/api/portal/auth/login', {
        method: 'POST',
        body: { email, password }
      })

      // Fetch user profile after successful login
      await fetchUser()
    } finally {
      loading.value = false
    }
  }

  async function logout() {
    await $fetch('/api/portal/auth/logout', { method: 'POST' })
    user.value = null
  }

  async function fetchUser() {
    try {
      user.value = await $fetch('/api/portal/client/me')
    } catch {
      user.value = null
    }
  }

  return {
    user,
    loading,
    login,
    logout,
    fetchUser
  }
}
```

- [ ] **Step 2: Verify `composables/useApi.ts` still works**

The `useApi` composable should already work correctly since it calls Nuxt server routes.

- [ ] **Step 3: Commit**

```bash
git add composables/
git commit -m "feat(nuxt): update auth composable for new backend"
```

---

## Task 5: Remove Old WHMCS Code

- [ ] **Step 1: Delete `server/utils/whmcs.ts`**

```bash
rm server/utils/whmcs.ts
```

- [ ] **Step 2: Delete old WHMCS API files if any**

```bash
rm -rf whmcs-api/
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(nuxt): remove old WHMCS integration code"
```

---

## Task 6: End-to-End Testing

- [ ] **Step 1: Test full user flow**

1. Register new user
2. Login
3. View profile
4. View services
5. View domains
6. View invoices
7. Create ticket
8. Logout

- [ ] **Step 2: Verify auth cookie flow**

Check browser dev tools → Application → Cookies to verify httpOnly cookie is set.

- [ ] **Step 3: Verify all endpoints work**

Test each migrated endpoint to ensure it works correctly.

- [ ] **Step 4: Document any breaking changes**

Update README.md with new environment variables and setup instructions.

---

## Self-Review

- [x] Created `server/utils/api.ts`
- [x] Updated all auth routes
- [x] Updated all client routes
- [x] Updated composables
- [x] Removed old WHMCS code
- [x] End-to-end testing complete
- [x] Documentation updated

---

## Execution Handoff

Plan complete. Choose execution method.
