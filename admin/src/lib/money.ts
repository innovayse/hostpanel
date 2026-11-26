/**
 * Money formatting and the currency vocabulary shared by every screen: the one symbol table, the
 * dollar formatters of the order, billing and dashboard screens, the price-book labels, and the
 * report currencies with their fixed conversion table.
 *
 * Everything that prints an amount goes through here, so two screens cannot disagree about a
 * symbol, a rounding or the shape of "12.50 USD".
 */
import { translate } from '@/lib/translate'
import { CurrencyCode } from '@/types/currency'

/** The currencies in the order the price-book dropdowns list them. */
export const CURRENCY_CODES: CurrencyCode[] = [
  CurrencyCode.Amd,
  CurrencyCode.Usd,
  CurrencyCode.Eur,
  CurrencyCode.Gbp,
  CurrencyCode.Rub,
]

/** The symbol printed before an amount, per currency. */
const CURRENCY_SYMBOLS: Record<CurrencyCode, string> = {
  [CurrencyCode.Amd]: '֏',
  [CurrencyCode.Usd]: '$',
  [CurrencyCode.Eur]: '€',
  [CurrencyCode.Gbp]: '£',
  [CurrencyCode.Rub]: '₽',
}

/** Closed set of currencies the report screens can display amounts in. */
export const ReportCurrency = {
  Usd: CurrencyCode.Usd,
  Eur: CurrencyCode.Eur,
  Rub: CurrencyCode.Rub,
  Amd: CurrencyCode.Amd,
} as const

/** One of the report currencies. */
export type ReportCurrency = (typeof ReportCurrency)[keyof typeof ReportCurrency]

/** Locale whose digit grouping and symbols currency amounts are printed in, whatever the interface language. */
const MONEY_FORMAT_LOCALE = 'en-US'

/**
 * Multiplier applied to a USD-denominated figure to restate it in the given currency. The rates
 * are fixed values; naming the closed set of currencies is what makes every lookup total, so a
 * conversion can never produce `NaN`.
 */
export const REPORT_CURRENCY_RATES: Record<ReportCurrency, number> = {
  USD: 1,
  EUR: 0.92,
  RUB: 90.5,
  AMD: 387,
}

/** Options for the currency `UiSelect` shown above a report; labels are read when shown. */
export const REPORT_CURRENCY_OPTIONS: { value: ReportCurrency; label: string }[] = [
  { value: ReportCurrency.Usd, get label() { return translate('reportCurrency.reportCurrencyOptions.usdUsDollar') } },
  { value: ReportCurrency.Eur, get label() { return translate('reportCurrency.reportCurrencyOptions.eurEuro') } },
  { value: ReportCurrency.Rub, get label() { return translate('reportCurrency.reportCurrencyOptions.rubRussianRuble') } },
  { value: ReportCurrency.Amd, get label() { return translate('reportCurrency.reportCurrencyOptions.amdArmenianDram') } },
]

/**
 * Writes a number with thousands grouping and a fixed number of decimals, so every price and amount of the panel reads the
 * same ("9,300.00", never "9300.00").
 *
 * @param amount - Numeric value.
 * @param decimals - Decimal places, always shown.
 * @returns For example "9,300.00".
 */
export const groupedAmount = (amount: number, decimals = 2): string =>
  amount.toLocaleString(MONEY_FORMAT_LOCALE, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })

/**
 * Writes a number with a fixed number of decimals and no thousands grouping, the text an editable amount field holds
 * ("23.70", never "23.7"): grouping would make the typed text unreadable to the parser.
 *
 * @param amount - Numeric value.
 * @param decimals - Decimal places, always shown.
 * @returns For example "9300.00".
 */
export const fixedAmount = (amount: number, decimals = 2): string =>
  amount.toLocaleString(MONEY_FORMAT_LOCALE, { minimumFractionDigits: decimals, maximumFractionDigits: decimals, useGrouping: false })

/**
 * The symbol for a currency code.
 *
 * @param code - ISO 4217 alpha code as the API sends it.
 * @returns The symbol, or `''` for a code this panel does not know.
 */
export const currencySymbol = (code: string): string => CURRENCY_SYMBOLS[code as CurrencyCode] ?? ''

/**
 * The dropdown label of a currency: its code and symbol, e.g. `EUR (€)`.
 *
 * @param code - The currency.
 * @returns The label.
 */
export const currencyLabel = (code: CurrencyCode): string => `${code} (${CURRENCY_SYMBOLS[code]})`

/**
 * Formats a large total in the currency the API names, with grouping and no decimals (dashboard tiles).
 *
 * @param amount - Amount in the given currency.
 * @param code - ISO 4217 currency code.
 * @returns For example "$12,500"; the rounded number and the code when the code is unknown.
 */
export const formatCurrencyWhole = (amount: number, code: string): string => {
  try {
    return new Intl.NumberFormat(MONEY_FORMAT_LOCALE, { style: 'currency', currency: code, maximumFractionDigits: 0 }).format(amount)
  } catch {
    // An unknown or malformed code makes Intl throw; show the plain number with the code.
    return `${groupedAmount(Math.round(amount), 0)} ${code}`
  }
}

/**
 * Formats an amount followed by the code of the currency it is in and no symbol, as the billing tables read.
 *
 * @param amount - Numeric value; a missing amount reads as zero.
 * @param currency - ISO 4217 currency code the amount is in; USD when the API did not say.
 * @returns For example "12.50 USD" or "53160.00 AMD".
 */
export const formatPlainAmount = (amount: number | null | undefined = 0, currency: string | null | undefined = CurrencyCode.Usd): string =>
  `${groupedAmount(amount ?? 0)} ${currency || CurrencyCode.Usd}`

/**
 * Formats a price with its currency symbol and code.
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns For example "$4.99 USD".
 */
export const formatPrice = (price: number, currency: string): string => `${currencySymbol(currency)}${groupedAmount(price)} ${currency}`

/**
 * Formats a price with its currency symbol only (a price-book cell).
 *
 * @param price - Numeric price value.
 * @param currency - ISO 4217 currency code.
 * @returns For example "$4.99"; no symbol for a code this panel does not know.
 */
export const formatSymbolPrice = (price: number, currency: string): string => `${currencySymbol(currency)}${groupedAmount(price)}`

/**
 * Formats an amount with the prefix, suffix and decimals a configured currency declares.
 *
 * @param amount - Numeric value.
 * @param currency - The currency's text before and after the number and its decimal places.
 * @param currency.prefix - Text before the number.
 * @param currency.suffix - Text after the number.
 * @param currency.decimals - Number of decimal places.
 * @returns For example "$2.99" or "2.99 ֏".
 */
export const formatAffixed = (amount: number, currency: { prefix: string; suffix: string; decimals: number }): string =>
  `${currency.prefix}${groupedAmount(amount, currency.decimals)}${currency.suffix}`

/**
 * Restates a USD figure in a report currency and formats it with two decimals.
 *
 * @param amount - Figure in USD.
 * @param currency - Currency to show it in; USD when omitted.
 * @param withCode - Appends the currency code after the number.
 * @returns For example `$12.00`, `€11.04` or `$12.00 USD`.
 */
export const formatReportAmount = (
  amount: number,
  currency: ReportCurrency = ReportCurrency.Usd,
  withCode = false,
): string => {
  const converted = amount * REPORT_CURRENCY_RATES[currency]
  const text = `${CURRENCY_SYMBOLS[currency]}${groupedAmount(converted)}`
  return withCode ? `${text} ${currency}` : text
}

/**
 * Formats an amount in the currency a report row carries.
 *
 * Used where the endpoint names the currency (any ISO code), unlike {@link formatReportAmount},
 * which restates a USD figure in one of the four display currencies.
 *
 * @param amount - Amount in the given currency.
 * @param code - ISO 4217 currency code.
 * @returns The amount with its currency symbol; the number and the code when the code is unknown.
 */
export const formatCurrencyCode = (amount: number, code: string): string => {
  try {
    return new Intl.NumberFormat(MONEY_FORMAT_LOCALE, { style: 'currency', currency: code }).format(amount)
  } catch {
    // An unknown or malformed code makes Intl throw; show the plain number with the code.
    return `${groupedAmount(amount)} ${code}`
  }
}

/**
 * Builds the money formatter of a report whose amounts the API converted to one base currency.
 *
 * The currency is read when an amount is formatted, so the formatter built before the report loads
 * follows the currency once it is known. Every report screen shares this one rule instead of
 * restating it.
 *
 * @param currency - Reads the base currency code the report names; empty until it loads.
 * @returns A function that formats an amount in that currency.
 */
export const baseCurrencyFormatter = (currency: () => string): ((amount: number) => string) =>
  (amount) => formatCurrencyCode(amount, currency())
