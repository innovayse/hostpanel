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
  cardSkeletonOf,
  cellDensityClasses,
  columnSizeOf,
  growControlClasses,
  headerDensityClasses,
  UiAlign,
  UiCardRole,
  UiColumnSize,
  UiHideBelow,
  UiSortDirection,
  withCardTitle,
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
  it('is a field unless the header says otherwise, whatever breakpoint it is hidden below', () => {
    expect(cardRoleOf({})).toBe(UiCardRole.Field)
    expect(cardRoleOf({ cardRole: UiCardRole.Control })).toBe(UiCardRole.Control)
    expect(cardRoleOf({ hideBelow: UiHideBelow.Lg })).toBe(UiCardRole.Field)
  })

  it('picks the growing column as the heading, else the first one, and never the checkbox or the actions', () => {
    const grow = withCardTitle([{ label: 'Id' }, { label: 'Name', size: UiColumnSize.Grow }])
    expect(grow.map(cardRoleOf)).toEqual([UiCardRole.Field, UiCardRole.Title])

    const first = withCardTitle([{ select: true }, { label: 'Id' }, { label: 'Name' }, { actions: true }])
    expect(first.map(cardRoleOf)).toEqual([UiCardRole.Field, UiCardRole.Title, UiCardRole.Field, UiCardRole.Field])
  })

  it('keeps the heading a header names and picks none for a table whose columns all name a role', () => {
    const named = withCardTitle([{ label: 'Id' }, { label: 'Name', cardRole: UiCardRole.Title }, { label: 'X', size: UiColumnSize.Grow }])
    expect(named.map(cardRoleOf)).toEqual([UiCardRole.Field, UiCardRole.Title, UiCardRole.Field])

    const controls = withCardTitle([{ cardRole: UiCardRole.Control }, { cardRole: UiCardRole.Control }])
    expect(controls.map(cardRoleOf)).toEqual([UiCardRole.Control, UiCardRole.Control])
  })

  it('boxes only a field, a control and a cell with no column, which the card places itself otherwise', () => {
    expect(cardCellClasses({})).toContain('justify-between')
    expect(cardCellClasses({ cardRole: UiCardRole.Control })).toContain('flex-col')
    expect(cardCellClasses(undefined)).toBe('w-full')
    expect(cardCellClasses({ cardRole: UiCardRole.Title })).toBe('')
    expect(cardCellClasses({ cardRole: UiCardRole.Badge })).toBe('')
    expect(cardCellClasses({ actions: true })).toBe('')
    expect(cardCellClasses({ select: true })).toBe('')
  })

  it('draws the caption of a field or control, hides a heading, subtitle or badge one for assistive technology, and none for the rest', () => {
    expect(cardLabelClasses({ label: 'Email' })).toContain('text-xs')
    expect(cardLabelClasses({ label: 'Email', cardRole: UiCardRole.Control })).toContain('text-xs')
    expect(cardLabelClasses({ label: 'Name', cardRole: UiCardRole.Title })).toBe('sr-only')
    expect(cardLabelClasses({ label: 'Mail', cardRole: UiCardRole.Subtitle })).toBe('sr-only')
    expect(cardLabelClasses({ label: 'Status', cardRole: UiCardRole.Badge })).toBe('sr-only')
    expect(cardLabelClasses({ label: 'Id', cardRole: UiCardRole.Hidden })).toBe('')
    expect(cardLabelClasses({ label: 'Pick', select: true })).toBe('')
    expect(cardLabelClasses({ label: 'Actions', actions: true })).toBe('')
    expect(cardLabelClasses({ label: '' })).toBe('')
    expect(cardLabelClasses(undefined)).toBe('')
  })

  it('describes the loading card by the parts the columns give it', () => {
    const shape = cardSkeletonOf(withCardTitle([
      { select: true },
      { label: 'Name' },
      { label: 'Email', cardRole: UiCardRole.Subtitle },
      { label: 'Status', cardRole: UiCardRole.Badge },
      { label: 'Id', cardRole: UiCardRole.Hidden },
      { label: 'Joined' },
      { label: 'Plan', cardRole: UiCardRole.Control },
      { actions: true },
    ]))

    expect(shape).toEqual({ select: true, subtitle: true, badge: true, actions: true, fields: 2 })
  })

  it('makes an actions column compact, whatever size it asks for', () => {
    expect(columnSizeOf({ label: 'Actions', actions: true })).toBe(columnSizeOf({ label: 'Id', size: UiColumnSize.Compact }))
    expect(columnSizeOf({ label: 'Actions', actions: true, size: UiColumnSize.Grow })).not.toContain('truncate')
  })

  it('truncates a growing column, except one of form controls, whose focus ring truncation would clip', () => {
    expect(columnSizeOf({ label: 'Name', size: UiColumnSize.Grow })).toContain('truncate')
    expect(columnSizeOf({ label: 'Description', size: UiColumnSize.Grow, cardRole: UiCardRole.Control })).toBe(growControlClasses)
    expect(growControlClasses).not.toContain('truncate')
  })
})
