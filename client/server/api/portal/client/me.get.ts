/**
 * GET /api/portal/client/me
 * Returns the authenticated client's profile.
 *
 * In SSO mode: proxies to the SSO /api/account/profile endpoint.
 * In local mode: calls the WHMCS-style C# backend /clients/me.
 */
// @ts-ignore
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
    const data = await res.json() as Record<string, unknown>

    return {
      id: data.id,
      firstname: data.firstName,
      lastname: data.lastName,
      email: data.email,
      phonenumber: data.phoneNumber ?? '',
      permissions: 8191,
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
    language: 'english',
    currency: undefined,
    currencyprefix: '',
    currencysuffix: '',
    permissions: 8191,
    email_preferences: {
      general: data.notifyGeneral ? 1 : 0,
      invoice: data.notifyInvoice ? 1 : 0,
      support: data.notifySupport ? 1 : 0,
      product: data.notifyProduct ? 1 : 0,
      domain: data.notifyDomain ? 1 : 0,
      affiliate: data.notifyAffiliate ? 1 : 0,
    },
  }
})
