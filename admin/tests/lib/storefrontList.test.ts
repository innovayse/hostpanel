/**
 * Tests for the storefront list schema helper: the columns the page hands to the table are the labelled columns that
 * are neither the handle nor the actions column, with the caption translated and the width, kind and card role kept, in order.
 */
import { describe, expect, it } from 'vitest'
import { storefrontListColumns } from '@/lib/storefrontList'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'
import { UiColumnKind } from '@/utils/ui/table'

/** A table head as a storefront list schema has it: the handle, two captions, the actions. */
const columns: readonly SkeletonColumn[] = [
  { key: 'handle' },
  { key: 'name', label: 'a.name', kind: UiColumnKind.Image },
  { key: 'status', label: 'a.status' },
  { key: 'actions', actions: true },
]

/**
 * Stands in for the `t` of `useI18n`.
 *
 * @param key - The locale key.
 * @returns The key marked as translated.
 */
const translate = (key: string): string => `t:${key}`

describe('storefrontListColumns', () => {
  it('translates the labelled columns in order and leaves out the handle and the actions', () => {
    expect(storefrontListColumns(columns, translate).map((column) => column.caption)).toEqual(['t:a.name', 't:a.status'])
  })

  it('keeps the kind the schema gives a column, so the table sizes it like the skeleton does', () => {
    expect(storefrontListColumns(columns, translate)[0]).toMatchObject({ key: 'name', kind: UiColumnKind.Image })
  })

  it('gives nothing for a table with no captioned column', () => {
    expect(storefrontListColumns([{ key: 'handle' }], translate)).toEqual([])
  })
})
