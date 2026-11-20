/**
 * Tests for the sidebar row helper: a group heading opens on the first entry of each group.
 */
import { describe, expect, it } from 'vitest'
import { sidebarRows } from '@/utils/ui/sidebar'
import type { UiSidebarItem } from '@/utils/ui/sidebar'

/**
 * Builds an entry.
 *
 * @param key - Its key.
 * @param group - Its group heading, if any.
 * @returns The entry.
 */
const entry = (key: string, group?: string): UiSidebarItem => ({ key, label: key, to: `/${key}`, icon: '', group })

describe('sidebarRows', () => {
  it('opens a heading on the first entry of each group only', () => {
    const rows = sidebarRows([entry('a', 'One'), entry('b', 'One'), entry('c', 'Two')])

    expect(rows.map(row => row.heading)).toEqual(['One', undefined, 'Two'])
  })

  it('draws no heading for entries without a group', () => {
    expect(sidebarRows([entry('a'), entry('b')]).map(row => row.heading)).toEqual([undefined, undefined])
  })
})
