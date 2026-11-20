/**
 * Unit tests for the quote line items card shared by the create and the quote screens: its table and its place as a card.
 */
import { describe, expect, it } from 'vitest'
import { QUOTE_ITEMS_PART, QUOTE_ITEMS_TABLE } from '@/lib/quoteSchemaParts'
import { SkeletonPartKind } from '@/utils/ui/skeletonSchema'

describe('quoteSchemaParts', () => {
  it('heads the line items with the seven columns of the editor, the last one the row actions', () => {
    expect(QUOTE_ITEMS_TABLE.columns.map((column) => column.key)).toEqual(['quantity', 'description', 'unitPrice', 'discount', 'taxed', 'total', 'actions'])
    expect(QUOTE_ITEMS_TABLE.columns.at(-1)?.actions).toBe(true)
  })

  it('is one card of line items holding that table and the Add item button', () => {
    const [card] = QUOTE_ITEMS_PART.columns.flat()

    expect(QUOTE_ITEMS_PART.kind).toBe(SkeletonPartKind.Cards)
    expect(QUOTE_ITEMS_PART.columns).toHaveLength(1)
    expect(card?.key).toBe('items')
    expect(card?.table).toBe(QUOTE_ITEMS_TABLE)
    expect(card?.actions).toBe(1)
  })
})
