/**
 * GET /api/portal/public/billing-currencies
 *
 * Proxies `GET /api/currencies` (anonymous) — the currencies a visitor may actually be billed
 * in, each with the `prefix`/`suffix`/`decimals` needed to format an amount, and no rate (the
 * storefront never converts, so a rate would only invite it to).
 *
 * This is deliberately a separate route from `public/currencies.get.ts`, which proxies
 * `/reference/currencies` — the static ISO 4217 reference table used by pickers such as the
 * client currency selector on an admin form. That table is not what a checkout offers; this
 * route is.
 *
 * @module server/api/portal/public/billing-currencies.get
 */
export default defineEventHandler(async (event) => {
  return await internalApiCall<unknown[]>(event, '/currencies')
})
