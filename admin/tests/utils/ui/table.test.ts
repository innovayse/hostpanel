/**
 * Unit tests for the table vocabulary: alignment, sort direction and density classes.
 */
import { describe, expect, it } from 'vitest'
import { UiTableDensity } from '@/utils/ui/injection'
import {
  alignClasses,
  cardCellClasses,
  cardLabelClasses,
  cardRoleOf,
  cellDensityClasses,
  headerDensityClasses,
  UiAlign,
  UiCardRole,
  UiHideBelow,
  UiSortDirection,
} from '@/utils/ui/table'

describe('table vocabulary', () => {
  it('maps every alignment to its logical text- class, so right-to-left layouts mirror', () => {
    expect(alignClasses[UiAlign.Left]).toBe('text-start')
    expect(alignClasses[UiAlign.Center]).toBe('text-center')
    expect(alignClasses[UiAlign.Right]).toBe('text-end')
  })

  it('uses the aria-sort strings for sort directions', () => {
    expect(Object.values(UiSortDirection)).toEqual(['none', 'ascending', 'descending'])
  })

  it.each([
    ['cell', cellDensityClasses],
    ['header', headerDensityClasses],
  ])('the %s map is empty for plain and padded otherwise', (_name, map) => {
    expect(map[UiTableDensity.Plain]).toBe('')
    expect(map[UiTableDensity.Compact]).toMatch(/px-/)
    expect(map[UiTableDensity.Comfortable]).toMatch(/h-1\d/)
  })
})

describe('card layout vocabulary', () => {
  it('is a field unless the header says otherwise', () => {
    expect(cardRoleOf({})).toBe(UiCardRole.Field)
    expect(cardRoleOf({ cardRole: UiCardRole.Control })).toBe(UiCardRole.Control)
  })

  it('leaves a column hidden below a breakpoint out of the card unless its header sets a role', () => {
    expect(cardRoleOf({ hideBelow: UiHideBelow.Lg })).toBe(UiCardRole.Hidden)
    expect(cardRoleOf({ hideBelow: UiHideBelow.Sm, cardRole: UiCardRole.Field })).toBe(UiCardRole.Field)
  })

  it('gives every role its own card classes and the select and actions columns theirs over any role', () => {
    expect(cardCellClasses({ cardRole: UiCardRole.Title })).toContain('max-sm:font-semibold')
    expect(cardCellClasses({ cardRole: UiCardRole.Badge })).toContain('max-sm:order-3')
    expect(cardCellClasses({ cardRole: UiCardRole.Control })).toContain('max-sm:flex-col')
    expect(cardCellClasses({ cardRole: UiCardRole.Hidden })).toBe('max-sm:hidden')
    expect(cardCellClasses({})).toContain('max-sm:justify-between')
    expect(cardCellClasses({ select: true, cardRole: UiCardRole.Title })).toContain('max-sm:order-1')
    expect(cardCellClasses({ actions: true, cardRole: UiCardRole.Title })).toContain('max-sm:border-t')
  })

  it('makes a cell with no column a full width line of the card', () => {
    expect(cardCellClasses(undefined)).toContain('max-sm:w-full')
  })

  it('draws the caption of a field or control, hides a title or badge one for assistive technology, and none for the rest', () => {
    expect(cardLabelClasses({ label: 'Email' })).toContain('sm:hidden')
    expect(cardLabelClasses({ label: 'Email', cardRole: UiCardRole.Control })).toContain('text-xs')
    expect(cardLabelClasses({ label: 'Name', cardRole: UiCardRole.Title })).toBe('sr-only sm:hidden')
    expect(cardLabelClasses({ label: 'Status', cardRole: UiCardRole.Badge })).toBe('sr-only sm:hidden')
    expect(cardLabelClasses({ label: 'Id', cardRole: UiCardRole.Hidden })).toBe('')
    expect(cardLabelClasses({ label: 'Pick', select: true })).toBe('')
    expect(cardLabelClasses({ label: 'Actions', actions: true })).toBe('')
    expect(cardLabelClasses({ label: '' })).toBe('')
    expect(cardLabelClasses(undefined)).toBe('')
  })
})
