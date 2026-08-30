/**
 * One currency, as `GET /api/portal/public/currencies` lists it.
 *
 * The endpoint proxies `/api/reference/currencies`, which returns `CurrencyList.All` — the
 * static ISO 4217 reference table in `Innovayse.Application.Admin.Common`, not the operator's
 * configured billing currencies. `CurrencyDto` is `(string Code, string Name, string Symbol)`
 * and that is the whole of it.
 *
 * This file used to declare `id`, `code`, `prefix` and `suffix`, a WHMCS currencies row. Three
 * of those four fields have never been sent, and `composables/useCurrency.ts` was built on them
 * — it looked up `c.id` and read `c.prefix`, so its `prefixFor()` returned `''` for every input
 * it was ever given. That composable had no callers and has been deleted rather than repaired:
 * `utils/formatCurrency.ts` already renders an amount from an ISO code, and a second formatter
 * disagreeing with it about the zero case was not worth keeping.
 *
 * @module types/portalcurrency
 */

/** One currency from the ISO 4217 reference table. */
export interface PortalCurrency {
  /** ISO 4217 code, e.g. `USD`. */
  code: string
  /** English display name, e.g. `US Dollar`. */
  name: string
  /** Currency symbol, e.g. `$`. */
  symbol: string
}
