/**
 * Pure helpers for the quote line editor: a blank line, a line built from a stored one, and the
 * request body lines without the editor's client-side key.
 */
import { nextRowKey } from '@/lib/rowKey'
import type { QuoteItemUpdate, QuoteLineDraft } from '@/types/quote'

/**
 * A blank line with a fresh key.
 *
 * @returns The blank line.
 */
export const blankQuoteLine = (): QuoteLineDraft => ({
  key: nextRowKey(),
  quantity: 1,
  description: '',
  unitPrice: 0,
  discountPercent: 0,
  taxed: false,
})

/**
 * Wraps a stored line for editing by giving it a fresh key.
 *
 * @param item - The stored line.
 * @returns The line as the editor holds it.
 */
export const quoteLineFrom = (item: QuoteItemUpdate): QuoteLineDraft => ({
  key: nextRowKey(),
  id: item.id,
  quantity: item.quantity,
  description: item.description,
  unitPrice: item.unitPrice,
  discountPercent: item.discountPercent,
  taxed: item.taxed,
})

/**
 * The lines as the API takes them, without the editor's key.
 *
 * @param lines - The lines held by the editor.
 * @returns The wire lines, in the same order.
 */
export const quoteLinesPayload = (lines: readonly QuoteLineDraft[]): QuoteItemUpdate[] =>
  lines.map((line) => ({
    id: line.id,
    quantity: line.quantity,
    description: line.description,
    unitPrice: line.unitPrice,
    discountPercent: line.discountPercent,
    taxed: line.taxed,
  }))
