/**
 * The arithmetic and formatting behind the TLD price book, shared by the TLD list and the TLD
 * form so the two cannot disagree about a margin or a price cell.
 */
import { formatSymbolPrice } from '@/lib/money'
import type { UiTextTone } from '@/utils/ui/text'
import { EMPTY_VALUE } from '@/lib/format'

/** The registration periods, in years, that a price is entered for. */
export const TLD_PERIODS = ['1', '2', '3', '5', '10'] as const

/**
 * A price map with a zero for every period.
 *
 * @returns A fresh map keyed by period, every value 0.
 */
export const emptyPriceMap = (): Record<string, number> =>
  Object.fromEntries(TLD_PERIODS.map((period) => [period, 0]))

/**
 * Fills the periods an API price map does not carry with zeros. A period the API sends that this
 * build does not list is kept untouched, so saving a TLD never drops a price it cannot display.
 *
 * @param map - Price map from the API.
 * @returns A full price map.
 */
export const fromApiMap = (map: Record<string, number>): Record<string, number> => {
  const base = emptyPriceMap()
  for (const [period, value] of Object.entries(map)) {
    base[period] = value
  }
  return base
}

/**
 * Keeps only the periods that carry a price, which is what the API stores.
 *
 * @param map - Price map from the form.
 * @returns A map with only the strictly positive entries.
 */
export const toNonZeroMap = (map: Record<string, number>): Record<string, number> => {
  const result: Record<string, number> = {}
  for (const [period, value] of Object.entries(map)) {
    if (value && value > 0) {
      result[period] = value
    }
  }
  return result
}

/**
 * Whether a cost and a sell price are both present, so a margin can be worked out.
 *
 * @param cost - Cost price, or undefined if the period has no value.
 * @param sell - Sell price, or undefined if the period has no value.
 * @returns True when both are positive numbers.
 */
const comparable = (cost: number | undefined, sell: number | undefined): cost is number =>
  Boolean(cost) && Boolean(sell)

/**
 * The margin between a cost and a sell price, as text.
 *
 * @param cost - Cost price, or undefined if the period has no value.
 * @param sell - Sell price, or undefined if the period has no value.
 * @returns A signed percentage like `+25.0%`, or an em dash when it cannot be calculated.
 */
export const calcMargin = (cost: number | undefined, sell: number | undefined): string => {
  if (!comparable(cost, sell) || sell === undefined) {
    return EMPTY_VALUE
  }
  const margin = ((sell - cost) / cost * 100).toFixed(1)
  return `${parseFloat(margin) >= 0 ? '+' : ''}${margin}%`
}

/**
 * The text tone of a margin: green when the sell price covers the cost, red when it does not.
 *
 * @param cost - Cost price, or undefined if the period has no value.
 * @param sell - Sell price, or undefined if the period has no value.
 * @returns The kit text tone.
 */
export const marginTone = (cost: number | undefined, sell: number | undefined): UiTextTone => {
  if (!comparable(cost, sell) || sell === undefined) {
    return 'muted'
  }
  return sell >= cost ? 'success' : 'danger'
}

/**
 * Formats a margin percentage the API already worked out.
 *
 * @param margin - Margin percentage, or null.
 * @returns A signed percentage like `+25.0%`, or an em dash when there is none.
 */
export const formatMarginPercent = (margin: number | null): string => {
  if (margin === null || margin === undefined) {
    return EMPTY_VALUE
  }
  return `${margin >= 0 ? '+' : ''}${margin.toFixed(1)}%`
}

/**
 * The text tone of a margin percentage.
 *
 * @param margin - Margin percentage, or null.
 * @returns Muted for none, green for zero or more, red for a loss.
 */
export const marginPercentTone = (margin: number | null): UiTextTone => {
  if (margin === null || margin === undefined) {
    return 'muted'
  }
  return margin >= 0 ? 'success' : 'danger'
}

/**
 * Formats a price with its currency symbol.
 *
 * @param price - Numeric price, or null.
 * @param currency - ISO 4217 currency code.
 * @returns The symbol and the price to two decimals, or an em dash when there is none.
 */
export const formatTldPrice = (price: number | null, currency: string): string => {
  if (price === null || price === undefined) {
    return EMPTY_VALUE
  }
  return formatSymbolPrice(price, currency)
}
