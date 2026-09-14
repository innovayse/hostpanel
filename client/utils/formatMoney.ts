/**
 * Formats an amount already known to be in one specific configured currency.
 *
 * `utils/formatCurrency.ts` covers the case where only an ISO code (or nothing) is known about
 * an amount's currency. This module covers the opposite, now-common case: the caller has a
 * full {@link MoneyCurrency} row — `prefix`, `suffix` and `decimals` — read from
 * `GET /api/currencies` or `GET /api/admin/currencies` through `stores/currency.ts`. Those
 * fields are the operator's own configuration, not a guess, so they are used directly instead
 * of asking `Intl` to infer them from an ISO code.
 *
 * @module utils/formatMoney
 */

/** The formatting is a fixed number of decimal places wrapped in the operator's own symbols. */
export interface MoneyCurrency {
  /** ISO 4217 alpha code, e.g. `AMD`. Not used for formatting — kept for callers that log it. */
  code?: string
  /** Text printed before the amount, e.g. `$`. */
  prefix?: string | null
  /** Text printed after the amount, e.g. ` ֏`. */
  suffix?: string | null
  /** Decimal places to show, e.g. `2` for USD, `0` for AMD. */
  decimals: number
}

/**
 * Formats a money amount in a specific configured currency.
 *
 * Grouping and decimal separators come from `Intl.NumberFormat`'s default locale handling
 * (digit grouping is not currency-specific, unlike the symbol and decimal count, which the
 * caller already supplied); the decimal *count* is pinned to `currency.decimals` rather than
 * left to `Intl`'s own currency table, because the operator's configuration — not a browser's
 * idea of what AMD does — is what actually printed on the price the visitor is paying.
 *
 * @param amount - The numeric amount, in the given currency. `null`/`undefined` renders as
 * an em dash, matching {@link EMPTY_AMOUNT} in `utils/formatCurrency.ts`.
 * @param currency - The currency's own prefix, suffix and decimal count.
 * @returns The formatted amount, e.g. `$2.99` or `1 200 ֏`.
 */
export const formatMoney = (
  amount: number | null | undefined,
  currency: MoneyCurrency
): string => {
  if (amount === null || amount === undefined || !Number.isFinite(amount)) return '—'

  const grouped = new Intl.NumberFormat(undefined, {
    minimumFractionDigits: currency.decimals,
    maximumFractionDigits: currency.decimals
  }).format(amount)

  return `${currency.prefix ?? ''}${grouped}${currency.suffix ?? ''}`
}
