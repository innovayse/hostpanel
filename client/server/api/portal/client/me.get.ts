/**
 * GET /api/portal/client/me
 * Returns the authenticated client's profile from the C# backend,
 * mapped to the WHMCS-style field names the frontend expects.
 *
 * The `permissions` field is a bit-flag integer (8191 = All).
 * Currently every user who can hit `/me` is the account owner,
 * so we return full permissions. When multi-user support is fully
 * active, this will call `ClientAccessService` to resolve per-user flags.
 */
export default defineEventHandler(async (event) => {
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
