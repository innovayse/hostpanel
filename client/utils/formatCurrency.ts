/**
 * Rendering a money amount without inventing the currency it is in.
 *
 * `/client/services/16` shows `24000` for a billing amount, and the dashboard shows `20000`
 * for invoice #8. Both are the same bug seen twice: no grouping, no decimals, no symbol.
 *
 * ## Where the currency actually comes from — checked, not assumed
 *
 * Three candidates were followed to their source. Only one of them was ever real:
 *
 * - **The client profile — this is the answer.** `ClientDto.Currency` in the C# API is a
 *   nullable ISO 4217 **code** (`backend/src/Innovayse.Application/Clients/Common/ClientDto.cs`).
 *   `server/api/portal/client/me.get.ts` used to discard it, hardcoding `currency: undefined,
 *   currencyprefix: '', currencysuffix: ''` in both its sso and local branches — which is why
 *   amounts rendered with no symbol in production: not a WHMCS quirk, a literal in the BFF. It
 *   now forwards the code, and `types/clientuser.ts` types `currency` as `string | null` rather
 *   than the *number* it claimed, which the backend has never sent.
 * - **The invoice — no.** `types/clientinvoice.ts` declared `currencycode` / `currencyprefix` /
 *   `currencysuffix` and pages rendered them, but `InvoiceDto` carries no currency field of any
 *   kind and the BFF routes are bare passthroughs, so all three were always `undefined`. Those
 *   readers now take the code off the account instead.
 * - **A portal setting — no.** `GET /api/portal/public/currencies` looks like the operator's
 *   configured billing currencies. It is not: it proxies `/api/reference/currencies`, which
 *   returns `CurrencyList.All`, the static ISO 4217 reference table `{ code, name, symbol }`.
 *   `composables/useCurrency.ts` looked up `c.id` and read `c.prefix` — no row has either — so
 *   its `prefixFor()` returned `''` for every input it was ever given. It had no callers and
 *   has been deleted; `types/portalcurrency.ts` now describes the shape actually served.
 *
 * **So the authoritative currency is `Client.Currency`, and it now reaches this file** as
 * `store.user?.currency`, passed as {@link CurrencyInfo.code}.
 *
 * ## What this does instead of guessing
 *
 * Nothing in the frontend knows whether `24000` is drams, roubles or dollars. Printing `֏`
 * because the visitor happens to be reading Armenian would put a currency on a bill that
 * nobody verified — and a wrong symbol on a bill is worse than none, because it is believed.
 *
 * `Intl.NumberFormat` gives grouping and decimals with no symbol at all, so an amount with no
 * known currency still reads as money: `24,000.00`, or `24 000,00` for a Russian or Armenian
 * reader. When the API *does* send a currency — an ISO code, or a bare prefix/suffix pair —
 * it is used, because then it came from the operator's configuration rather than from a guess
 * made here.
 *
 * @module utils/formatCurrency
 */

/**
 * What an amount renders as when there is no number to render.
 *
 * The same em dash `utils/formatDate.ts` uses, so an empty row reads consistently down a
 * description list whichever kind of field is missing.
 */
export const EMPTY_AMOUNT = '—'

/** Well-formed ISO 4217: three letters. Anything else is not a code `Intl` will accept. */
const ISO_4217 = /^[A-Za-z]{3}$/

/**
 * Everything the API might have told us about the currency an amount is in.
 *
 * Every field is optional and every field is routinely absent — see the module note. Passing
 * an empty object is the normal case today, not a degenerate one.
 */
export interface CurrencyInfo {
  /**
   * ISO 4217 code, e.g. `AMD`. The only field that yields a properly localised currency
   * rendering, because it is the only one `Intl.NumberFormat` can place and pluralise itself.
   */
  code?: string | null

  /** Symbol the operator configured to print before the amount, e.g. `$`. */
  prefix?: string | null

  /** Symbol the operator configured to print after the amount, e.g. ` AMD`. */
  suffix?: string | null

  /**
   * `@nuxtjs/i18n` locale code, deciding the grouping and decimal separators. Omit inside a
   * component to use the locale of the request being rendered.
   */
  locale?: string
}

/**
 * Reads the language to group digits in, without a module-level cache.
 *
 * Same reasoning as `utils/formatDate.ts`: a module-scoped "current locale" is shared by every
 * visitor a Nitro process is serving at once, so the locale is resolved per call from the Nuxt
 * instance of the request being rendered.
 *
 * @param locale - What the caller passed, if anything.
 * @returns The caller's locale, else the current request's, else `en`.
 */
const resolveLocale = (locale?: string): string => {
  if (locale) return locale

  try {
    const i18n = (useNuxtApp() as { $i18n?: { locale?: { value?: string } } }).$i18n
    return i18n?.locale?.value ?? 'en'
  }
  catch {
    return 'en'
  }
}

/**
 * Coerces an amount the API sent into a number, or `null` when it is not one.
 *
 * @param amount - The field as it arrived. WHMCS-shaped endpoints send decimal *strings*
 *                 (`"24000.00"`), so a string is the common case rather than the exception.
 * @returns The numeric value, or `null` for anything absent or unparseable.
 */
const toNumber = (amount: string | number | null | undefined): number | null => {
  if (amount === null || amount === undefined || amount === '') return null

  const value = typeof amount === 'number' ? amount : Number(amount)
  return Number.isFinite(value) ? value : null
}

/**
 * Formats a money amount, using a currency only when the API supplied one.
 *
 * Three outcomes, in order of how much the API told us:
 *
 * - An ISO 4217 `code` — `Intl` places the symbol and picks the minor-unit count itself, so
 *   `AMD` renders with no decimals and `USD` with two, in the right position for the locale.
 * - A `prefix` and/or `suffix` and no code — the operator's own symbols wrap a grouped number.
 * - Neither — a grouped number with two decimals and **no symbol**. This is the honest answer
 *   and currently the production one; see the module note for why guessing is worse.
 *
 * @param amount - The amount, as a number or the decimal string the API sends.
 * @param info - What is known about the currency. Omit entirely for the no-currency case.
 * @returns The formatted amount, or {@link EMPTY_AMOUNT} when there is no number to show.
 */
export const formatCurrency = (
  amount: string | number | null | undefined,
  info: CurrencyInfo = {}
): string => {
  const value = toNumber(amount)
  if (value === null) return EMPTY_AMOUNT

  const locale = resolveLocale(info.locale)
  const code = info.code?.trim()

  if (code && ISO_4217.test(code)) {
    try {
      return new Intl.NumberFormat(locale, { style: 'currency', currency: code.toUpperCase() })
        .format(value)
    }
    catch {
      // `Intl` accepts any three-letter code and only throws on a malformed one, which the
      // regex above already rejects — so this is belt and braces against an ICU build that is
      // stricter. Falling through loses the symbol; throwing would lose the amount.
    }
  }

  const grouped = new Intl.NumberFormat(locale, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(value)

  const prefix = info.prefix ?? ''
  const suffix = info.suffix ?? ''
  return `${prefix}${grouped}${suffix}`
}
