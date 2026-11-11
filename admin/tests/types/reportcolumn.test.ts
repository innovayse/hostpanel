/**
 * Tests for isNumericColumn: which report columns take the numeric shape (right aligned, figures of one width).
 */
import { describe, expect, it } from 'vitest'
import { ReportCellFormat, isNumericColumn } from '@/types/reportcolumn'

describe('isNumericColumn', () => {
  it('treats the money, percent, integer and years formats as numbers', () => {
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Money })).toBe(true)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Percent })).toBe(true)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Integer })).toBe(true)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Years })).toBe(true)
  })

  it('leaves text, date, status, id and unformatted columns as text', () => {
    expect(isNumericColumn({ key: 'a', label: 'A' })).toBe(false)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Text })).toBe(false)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Date })).toBe(false)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Status })).toBe(false)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Id })).toBe(false)
  })

  it('lets the column flag win over the format', () => {
    expect(isNumericColumn({ key: 'a', label: 'A', numeric: true })).toBe(true)
    expect(isNumericColumn({ key: 'a', label: 'A', format: ReportCellFormat.Money, numeric: false })).toBe(false)
  })
})
