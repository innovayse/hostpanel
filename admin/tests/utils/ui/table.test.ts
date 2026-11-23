/**
 * Unit tests for the table vocabulary: alignment, sort direction and density classes.
 */
import { describe, expect, it } from 'vitest'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiTableColumn } from '@/utils/ui/injection'
import {
  alignClasses,
  cardCellClasses,
  cardLabelClasses,
  cardRoleOf,
  cardSkeletonOf,
  cellDensityClasses,
  columnSizeOf,
  sizeOfColumn,
  truncatesColumn,
  growControlClasses,
  headerDensityClasses,
  UiAlign,
  UiCardRole,
  UiColumnKind,
  UiColumnSize,
  UiColumnWidth,
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

  it('makes the first status column the badge of the card unless a column already names one', () => {
    /** The card roles the columns end up with once the card title is chosen. */
    const roles = (columns: UiTableColumn[]) => withCardTitle(columns).map((column) => column.cardRole)

    expect(roles([{ label: 'Name' }, { label: 'Status', kind: UiColumnKind.Status }, { label: 'Flag', kind: UiColumnKind.Status }]))
      .toEqual([UiCardRole.Title, UiCardRole.Badge, undefined])
    expect(roles([{ label: 'Name' }, { label: 'State', kind: UiColumnKind.Status }, { label: 'Own', cardRole: UiCardRole.Badge }]))
      .toEqual([UiCardRole.Title, undefined, UiCardRole.Badge])
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

  describe('column sizing', () => {
    it('gives free text a double share and a status, a date, an id or a number a single one', () => {
      expect(sizeOfColumn({ label: 'Name' })).toBe(UiColumnSize.Grow)
      expect(sizeOfColumn({ label: 'Name', kind: UiColumnKind.Text })).toBe(UiColumnSize.Grow)
      for (const kind of [UiColumnKind.Status, UiColumnKind.Date, UiColumnKind.Id]) {
        expect(sizeOfColumn({ label: 'x', kind })).toBe(UiColumnSize.Narrow)
      }
      expect(sizeOfColumn({ numeric: true })).toBe(UiColumnSize.Narrow)
    })

    it('never hands all the free width to one column: every data column claims a percentage share', () => {
      const shares = [UiColumnSize.Grow, UiColumnSize.Narrow].map((size) => columnSizeOf({ size }))

      expect(shares[0]).toContain('w-[28%]')
      expect(shares[1]).toContain('w-[14%]')
      expect(shares.join(' ')).not.toContain('w-full')
    })

    it('keeps an actions column compact and lets a named size win over the kind', () => {
      expect(sizeOfColumn({ actions: true, kind: UiColumnKind.Text })).toBe(UiColumnSize.Compact)
      expect(sizeOfColumn({ kind: UiColumnKind.Date, size: UiColumnSize.Auto })).toBe(UiColumnSize.Auto)
      expect(columnSizeOf({ actions: true })).toContain('w-px')
    })

    it('gives a fixed column the class of its width, a medium one when none is named', () => {
      expect(columnSizeOf({ size: UiColumnSize.Fixed, width: UiColumnWidth.Small })).toContain('w-24')
      expect(columnSizeOf({ size: UiColumnSize.Fixed, width: UiColumnWidth.Large })).toContain('w-64')
      expect(columnSizeOf({ size: UiColumnSize.Fixed })).toContain('w-40')
    })

    it('truncates only a growing column of text', () => {
      expect(truncatesColumn({ label: 'Name' })).toBe(true)
      expect(truncatesColumn({ kind: UiColumnKind.Date })).toBe(false)
      expect(truncatesColumn({ size: UiColumnSize.Grow, cardRole: UiCardRole.Control })).toBe(false)
      expect(truncatesColumn({ actions: true })).toBe(false)
      expect(truncatesColumn(undefined)).toBe(false)
    })
  })
})
