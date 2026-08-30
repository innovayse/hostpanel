/**
 * PUT /api/portal/client/me
 * Updates the authenticated client's profile via the C# backend.
 */

/**
 * The account form's own field names, as the page posts them. They are the WHMCS-era
 * names the client area was built against, not the backend's, so nothing here binds by
 * accident -- every one of them is mapped below.
 */
interface AccountFormBody {
  firstname?: string
  lastname?: string
  companyname?: string
  email?: string
  phonenumber?: string
  address1?: string
  address2?: string
  city?: string
  state?: string
  postcode?: string
  country?: string
  paymentmethod?: string
  /**
   * Posted by the Language dropdown, as one of the codes in `LocaleOptions.SupportedLocales`
   * (`en`, `ru`, `hy`) or empty for "no preference". Forwarded: the backend stores it on the
   * account row, under `AUTH_MODE=local` where this deployment owns that row.
   */
  language?: string
  emailprefs?: Record<string, boolean>
}

/**
 * Picks the first value that was actually supplied, falling back to what is stored.
 *
 * @param posted - What the form sent for this field, or undefined when it holds no such field.
 * @param current - What the record holds today.
 * @returns The value to send to the backend.
 */
const pick = <T>(posted: T | undefined, current: T | undefined): T | undefined =>
  posted !== undefined ? posted : current

export default defineEventHandler(async (event) => {
  const body = await readBody<AccountFormBody>(event)

  // Read before write, and send the whole record back.
  //
  // The backend's update command is a full replacement: every field it does not receive is
  // written as null or false, because `Client.UpdatePreferences`, `UpdateNotifications` and
  // `UpdateSettings` are plain setters with no "absent means unchanged" notion. The account
  // form only carries a third of those fields and posts them under different names, so
  // forwarding its body verbatim -- as this route used to -- cleared the client's phone,
  // street, currency, admin notes, all six notification preferences and all eight billing
  // settings on every save. Merging here keeps that contract honest without changing what a
  // full replacement means to the admin screens that legitimately rely on it.
  const current = await internalApiCall<Record<string, unknown>>(event, '/clients/me')

  const prefs = body.emailprefs

  // The dropdown's empty option means "no preference", which the account row stores as null
  // rather than as an empty string -- an empty string is a language code that matches none of
  // the supported locales and would be served English while looking like a saved choice.
  // `undefined` is left alone so the merge below falls back to what is stored.
  const language = body.language === undefined ? undefined : (body.language || null)

  return await internalApiCall<Record<string, unknown>>(event, '/clients/me', {
    method: 'PUT',
    body: {
      Email: pick(body.email, current.email as string | undefined),
      FirstName: pick(body.firstname, current.firstName as string | undefined),
      LastName: pick(body.lastname, current.lastName as string | undefined),
      CompanyName: pick(body.companyname, current.companyName as string | undefined),
      Phone: pick(body.phonenumber, current.phone as string | undefined),
      Street: pick(body.address1, current.street as string | undefined),
      Address2: pick(body.address2, current.address2 as string | undefined),
      City: pick(body.city, current.city as string | undefined),
      State: pick(body.state, current.state as string | undefined),
      PostCode: pick(body.postcode, current.postCode as string | undefined),
      Country: pick(body.country, current.country as string | undefined),
      // Read back like every other field on this form, so a save that did not touch the
      // language does not arrive at the handler looking like a change. Under `AUTH_MODE=sso`
      // the backend answers null here and accepts null back, so nothing is written and
      // nothing is refused.
      Language: pick(language, current.language as string | null | undefined) ?? null,
      Currency: current.currency,
      PaymentMethod: pick(body.paymentmethod, current.paymentMethod as string | undefined),
      BillingContact: current.billingContact,
      // Admin-only, and not on this form at all: read back so a client save cannot erase it.
      AdminNotes: current.adminNotes,
      NotifyGeneral: pick(prefs?.general, current.notifyGeneral as boolean | undefined),
      NotifyInvoice: pick(prefs?.invoice, current.notifyInvoice as boolean | undefined),
      NotifySupport: pick(prefs?.support, current.notifySupport as boolean | undefined),
      NotifyProduct: pick(prefs?.product, current.notifyProduct as boolean | undefined),
      NotifyDomain: pick(prefs?.domain, current.notifyDomain as boolean | undefined),
      NotifyAffiliate: pick(prefs?.affiliate, current.notifyAffiliate as boolean | undefined),
      LateFees: current.lateFees,
      OverdueNotices: current.overdueNotices,
      TaxExempt: current.taxExempt,
      SeparateInvoices: current.separateInvoices,
      DisableCcProcessing: current.disableCcProcessing,
      MarketingOptIn: current.marketingOptIn,
      StatusUpdate: current.statusUpdate,
      AllowSso: current.allowSso,
      // Never from the portal: a client does not get to set their own account status, and
      // null is what the command reads as "leave it alone".
      Status: null,
    },
  })
})
