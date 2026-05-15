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

    // Validate the token against the backend before trusting it
    try {
      const data = await request<{ verified: boolean }>('/auth/email-verified')
      user.value = parsed
      emailVerified.value = data.verified
    } catch {
      // Token was rejected (revoked, expired, etc.) — clear session
      clearToken()
      user.value = null
      emailVerified.value = null
    }
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
