/**
 * Tests for the storefront list schema helper: the captions the page hands to the table are the labelled columns that
 * are neither the handle nor the actions column, translated, in order.
 */
import { describe, expect, it } from 'vitest'
import { storefrontColumnCaptions } from '@/lib/storefrontList'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'

/** A table head as a storefront list schema has it: the handle, two captions, the actions. */
const columns: readonly SkeletonColumn[] = [
  { key: 'handle' },
  { key: 'name', label: 'a.name' },
  { key: 'status', label: 'a.status' },
  { key: 'actions', label: 'shared.actions', actions: true },
]

/**
 * Stands in for the `t` of `useI18n`.
 *
 * @param key - The locale key.
 * @returns The key marked as translated.
 */
const translate = (key: string): string => `t:${key}`

describe('storefrontColumnCaptions', () => {
  it('translates the labelled columns in order and leaves out the handle and the actions', () => {
    expect(storefrontColumnCaptions(columns, translate)).toEqual(['t:a.name', 't:a.status'])
  })

  it('gives nothing for a table with no captioned column', () => {
    expect(storefrontColumnCaptions([{ key: 'handle' }], translate)).toEqual([])
  })
})
