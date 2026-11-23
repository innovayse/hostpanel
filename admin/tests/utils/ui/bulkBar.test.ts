/**
 * Tests of the bulk bar action: it is a bar action with a stable key and a full-sentence title, so the shared bar
 * helpers work on it unchanged.
 */
import { describe, expect, it } from 'vitest'
import { barActionToMenu, primaryIndex } from '@/utils/ui/actionBar'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiBulkAction } from '@/utils/ui/bulkBar'

/**
 * Builds a bulk action with sensible defaults.
 *
 * @param overrides - Fields that replace the defaults.
 * @returns A complete bulk action.
 */
const action = (overrides: Partial<UiBulkAction> = {}): UiBulkAction => ({
  key: 'delete',
  label: 'Delete',
  title: 'Delete the selected clients',
  icon: '<path d="M0 0" />',
  variant: UiButtonVariant.Danger,
  testid: 'bulk-delete',
  ...overrides,
})

describe('UiBulkAction', () => {
  it('carries the key the bar emits and the full title next to the short label', () => {
    const bulk = action()
    expect(bulk.key).toBe('delete')
    expect(bulk.label).toBe('Delete')
    expect(bulk.title).toBe('Delete the selected clients')
  })

  it('keeps the primary action of the bar on a phone', () => {
    const actions = [action(), action({ key: 'export', variant: UiButtonVariant.Primary, testid: 'bulk-export' })]
    expect(primaryIndex(actions)).toBe(1)
  })

  it('folds into a menu row named by its title, with the danger look and the disabled state', () => {
    expect(barActionToMenu(action({ loading: true }))).toEqual({
      label: 'Delete the selected clients',
      icon: '<path d="M0 0" />',
      danger: true,
      disabled: true,
      testid: 'bulk-delete',
    })
  })
})
