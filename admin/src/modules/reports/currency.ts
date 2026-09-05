/**
 * The currency a report screen can restate its figures in, and the fixed conversion
 * table those screens share.
 *
 * Every reports view carried its own copy of these four maps. Beyond the duplication, the
 * copies were typed `Record<string, …>`, which made `rates[selectedCurrency.value]` a
 * possibly-`undefined` read on a value that is in fact always one of the four keys. Naming
 * the union here is what lets the lookup be total, so the multiplication in each `fmt`
 * can no longer silently produce `NaN`.
 *
 * The rates are hard-coded, exactly as they were in each view — this module only moves them.
 */

/** A currency the reports screens can display amounts in. */
export type ReportCurrency = 'USD' | 'EUR' | 'RUB' | 'AMD'

/** Multiplier applied to a USD-denominated figure to restate it in the given currency. */
export const REPORT_CURRENCY_RATES: Record<ReportCurrency, number> = {
  USD: 1,
  EUR: 0.92,
  RUB: 90.5,
  AMD: 387,
}

/** Symbol prefixed to a converted amount. */
export const REPORT_CURRENCY_SYMBOLS: Record<ReportCurrency, string> = {
  USD: '$',
  EUR: '€',
  RUB: '₽',
  AMD: '֏',
}

/** Options for the currency `UiSelect` shown above a report. */
export const REPORT_CURRENCY_OPTIONS: { value: ReportCurrency; label: string }[] = [
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'RUB', label: 'RUB — Russian Ruble' },
  { value: 'AMD', label: 'AMD — Armenian Dram' },
]
