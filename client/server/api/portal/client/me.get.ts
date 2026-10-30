/**
 * GET /api/portal/client/me
 * Returns the authenticated client's profile: the backend `MyProfileDto` as it is sent, plus the
 * caller's `permissions`, `actsForAccount` and `hasClientProfile`.
 *
 * In SSO mode: the profile is the same record `PATCH` (me.patch.ts) writes to, with `email` and
 * `twoFactorEnabled` taken from SSO's own /api/account/profile, where sign-in and 2FA live.
 * A staff/admin identity with no linked client record (/clients/me answers 403 when the
 * identity lacks the Client role, 404 when it has the role but no record) gets the identity's
 * name, email and phone with every billing field null, and `hasClientProfile: false`
 * so the portal can show the "not a customer account" notice without first sending every
 * customer-only request and logging the browser's console error for each refusal.
 *
 * In local mode: the profile from /clients/me, untouched.
 *
 * ## Permissions
 *
 * `permissions` is the `ClientPermission` integer the API reports for the signed-in person
 * (`clientPermissions` on `GET /auth/me`): everything for the owner of a client record, the stored
 * grant for a linked user, nothing for anyone else. It only decides what the portal shows (the API
 * enforces the same flags on every request), so when `/auth/me` cannot be read the value is 0
 * (show nothing that needs a permission) rather than a guess that grants.
 *
 * `actsForAccount` says whether those flags apply to the person at all: true when `/auth/me` names a
 * client account (`clientId`) or an external account for the caller, false for a signed-in person
 * with neither, such as a first-time buyer whose account the first order creates. The flags only
 * restrict members of an account, so the portal offers such a person the purchase. It is derived
 * here, once, so no page repeats the rule. When `/auth/me` cannot be read, or an older backend
 * sends neither field, the answer is true: unknown counts as an account, the side that shows less.
 *
 * @module server/api/portal/client/me.get
 */

import type { H3Event } from 'h3'
import { AuthMode } from '~/types/authmode'
import type { ClientUser } from '~/types/clientuser'
import type { MyProfile } from '~/types/myprofile'
import { resolveAccessToken } from '~/server/utils/portal-session'
import { signOnFetch } from '~/server/utils/signon-http'

/** The part of the single sign-on `GET /api/account/profile` answer this route reads. */
interface SsoIdentity {
  /** Sign-in email of the identity. */
  email: string
  /** Given name held by the identity provider. */
  firstName: string
  /** Family name held by the identity provider. */
  lastName: string
  /** Phone number held by the identity provider, when one is set. */
  phoneNumber?: string | null
  /** Whether TOTP two-factor authentication is switched on for the identity. */
  totpEnabled?: boolean
}

/** Statuses `/clients/me` answers with when the identity is not a customer account (no Client role, no client record). */
const NOT_A_CUSTOMER_STATUSES: ReadonlySet<number> = new Set([403, 404])

/** The part of `GET /auth/me` this route reads. */
interface BackendAccount {
  /** The `ClientPermission` flags the caller holds on a client account, as an integer. */
  clientPermissions?: number
  /** The client account the caller acts for; null when they act for none, absent on an older backend. */
  clientId?: number | null
  /** The account in another system the client is linked to; null when there is none, absent on an older backend. */
  externalAccount?: { provider: string, id: string } | null
}

/** What the portal reads of the caller's access to a client account. */
type ClientAccess = Pick<ClientUser, 'permissions' | 'actsForAccount'>

/**
 * Reads the permissions the signed-in person holds and whether they act for a client account.
 *
 * @param event - The incoming browser request, whose session the backend call carries.
 * @returns The permission integer (0 when the backend does not answer or sends no value) and
 *          whether the person acts for an account (true when that is unknown).
 */
const readClientAccess = async (event: H3Event): Promise<ClientAccess> => {
  try {
    const account = await internalApiCall<BackendAccount>(event, '/auth/me')
    const permissions = typeof account.clientPermissions === 'number' ? account.clientPermissions : 0
    const accountKnown = account.clientId !== undefined || account.externalAccount !== undefined
    const actsForAccount = !accountKnown || account.clientId != null || account.externalAccount != null
    return { permissions, actsForAccount }
  } catch {
    return { permissions: 0, actsForAccount: true }
  }
}

export default defineEventHandler(async (event): Promise<ClientUser> => {
  const config = useRuntimeConfig()
  const authMode = config.authMode as string ?? AuthMode.Sso

  if (authMode === AuthMode.Sso) {
    const accessToken = await resolveAccessToken(event)
    if (!accessToken) {
      throw createError({ statusCode: 401 })
    }

    const identity = await signOnFetch<SsoIdentity>('/api/account/profile', { accessToken })

    /** True only when the backend said this identity is not a customer; any other failure leaves it unknown. */
    let notACustomer = false
    const billing = await internalApiCall<MyProfile>(event, '/clients/me')
      .catch((err: { statusCode?: number }) => {
        notACustomer = NOT_A_CUSTOMER_STATUSES.has(err?.statusCode ?? 0)
        return null
      })

    const access = await readClientAccess(event)
    // SSO owns sign-in and 2FA in this mode, so its email and totpEnabled, not anything the
    // client record might carry, are the one answer for who signs in and whether 2FA is on.
    const twoFactorEnabled = identity.totpEnabled === true
    const email = identity.email

    if (billing) {
      return { ...billing, email, twoFactorEnabled, ...access, hasClientProfile: true }
    }

    // No client record to pass through: the identity alone, with every billing field empty.
    // `hasClientProfile` is false only on a definite "not a customer" answer; a transient
    // failure leaves it true (unknown), so the customer sections still get their chance to load.
    return {
      id: null,
      email,
      firstName: identity.firstName,
      lastName: identity.lastName,
      companyName: null,
      phone: identity.phoneNumber ?? null,
      address: null,
      language: null,
      currency: null,
      paymentMethod: null,
      notifications: null,
      twoFactorEnabled,
      contacts: [],
      ...access,
      hasClientProfile: !notACustomer,
    }
  }

  const data = await internalApiCall<MyProfile>(event, '/clients/me')

  return { ...data, ...(await readClientAccess(event)), hasClientProfile: true }
})
