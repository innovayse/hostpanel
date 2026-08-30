/**
 * GET /api/portal/client/me
 * Returns the authenticated client's profile.
 *
 * In SSO mode: identity (name, email, 2FA) comes from SSO's own /api/account/profile --
 * that is where sign-in and 2FA actually live, so it is the one source of truth for both.
 * Billing fields (company, address, payment method, language, email preferences) still
 * come from the WHMCS-style C# backend /clients/me, the same record `PUT` (below) writes
 * to -- without this, a real sso-mode client's edits would save but never read back.
 * A staff/admin identity with no linked client record (checked via /clients/me 400/404)
 * gets identity fields only, with billing fields left at their empty defaults.
 *
 * In local mode: everything, including identity, comes from /clients/me.
 *
 * ## Currency
 *
 * `currency` is forwarded from `ClientDto.Currency`, which is a nullable ISO 4217 **code**
 * (`AMD`, `USD`). Both branches used to hardcode `currency: undefined, currencyprefix: '',
 * currencysuffix: ''`, discarding the one authoritative answer the backend had already sent —
 * which is why every amount in the portal rendered without a symbol in production. There is
 * no prefix or suffix anywhere in the API, so the two symbol fields are gone rather than
 * emptied: `utils/formatCurrency.ts` places the symbol from the code itself.
 */
export default defineEventHandler(async (event) => {
  const config = useRuntimeConfig()
  const authMode = config.authMode as string ?? 'sso'

  if (authMode === 'sso') {
    const accessToken = getCookie(event, 'auth_token')
    if (!accessToken) throw createError({ statusCode: 401 })

    const res = await fetch(`${config.ssoUrl}/api/account/profile`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    })
    if (!res.ok) throw createError({ statusCode: res.status })
    const identity = await res.json() as Record<string, unknown>

    const billing = await internalApiCall<Record<string, unknown>>(event, '/clients/me')
      .catch(() => null)

    return {
      id: identity.id,
      firstname: (billing?.firstName as string | undefined) ?? identity.firstName,
      lastname: (billing?.lastName as string | undefined) ?? identity.lastName,
      companyname: billing?.companyName,
      email: identity.email,
      phonenumber: (billing?.phone as string | undefined) ?? identity.phoneNumber ?? '',
      address1: billing?.street,
      address2: billing?.address2,
      city: billing?.city,
      state: billing?.state,
      postcode: billing?.postCode,
      country: billing?.country,
      defaultgateway: billing?.paymentMethod,
      // Null, not a language. Where the SSO owns the person it owns their chosen language
      // too, and neither `/api/account/profile` nor hostpanel's own record carries one, so
      // there is nothing here to answer with. It used to answer `'english'`, which showed a
      // stored preference nobody had ever set and no save could ever change.
      language: null,
      // The account's billing currency, ISO 4217. Absent when this deployment holds no client
      // record for the identity -- a staff login, say -- which is not the same as "no currency
      // configured", and both correctly render an amount with no symbol rather than a guess.
      currency: (billing?.currency as string | null | undefined) ?? null,
      permissions: 8191,
      email_preferences: billing && {
        general: billing.notifyGeneral ? 1 : 0,
        invoice: billing.notifyInvoice ? 1 : 0,
        support: billing.notifySupport ? 1 : 0,
        product: billing.notifyProduct ? 1 : 0,
        domain: billing.notifyDomain ? 1 : 0,
        affiliate: billing.notifyAffiliate ? 1 : 0,
      },
      // SSO owns sign-in and 2FA in this mode, so its totpEnabled -- not anything the
      // client record might carry -- is the one answer for whether it's actually on.
      twoFactorEnabled: identity.totpEnabled ?? false,
    }
  }

  const data = await internalApiCall<Record<string, unknown>>(event, '/clients/me')

  return {
    id: data.id,
    firstname: data.firstName,
    lastname: data.lastName,
    companyname: data.companyName,
    email: data.email,
    phonenumber: data.phone,
    address1: data.street,
    address2: data.address2,
    city: data.city,
    state: data.state,
    postcode: data.postCode,
    country: data.country,
    defaultgateway: data.paymentMethod,
    // From the record, now that the backend actually sends it: `/clients/me` reads it off
    // the account row this deployment owns. It used to be hard-coded to `'english'`, which
    // is why the Language dropdown appeared to save and always read back as English.
    language: data.language ?? '',
    // The account's billing currency, ISO 4217, straight off the record. See the module note.
    currency: (data.currency as string | null | undefined) ?? null,
    permissions: 8191,
    email_preferences: {
      general: data.notifyGeneral ? 1 : 0,
      invoice: data.notifyInvoice ? 1 : 0,
      support: data.notifySupport ? 1 : 0,
      product: data.notifyProduct ? 1 : 0,
      domain: data.notifyDomain ? 1 : 0,
      affiliate: data.notifyAffiliate ? 1 : 0,
    },
    // Passed through rather than fetched separately: the account screen used to ask
    // /api/portal/auth/2fa-status for this, a route that exists on neither side, so the badge
    // read "Disabled" for everyone. The profile has carried the flag all along.
    twoFactorEnabled: data.twoFactorEnabled ?? false,
  }
})
