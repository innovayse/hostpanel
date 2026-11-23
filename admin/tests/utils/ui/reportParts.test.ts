/**
 * Tests of the report schema vocabulary: the header buttons and the closing notes a report can name.
 */
import { describe, expect, it } from 'vitest'
import { ReportAction, ReportNote } from '@/utils/ui/reportParts'

describe('ReportAction', () => {
  it('lists the header buttons in the order a schema lists them', () => {
    expect(Object.values(ReportAction)).toEqual(['refresh', 'export', 'print'])
  })

  it('exposes the named members as their literal values', () => {
    expect(ReportAction.Refresh).toBe('refresh')
    expect(ReportAction.Export).toBe('export')
    expect(ReportAction.Print).toBe('print')
  })

  it('has no two buttons with the same value', () => {
    const values = Object.values(ReportAction)
    expect(new Set(values).size).toBe(values.length)
  })
})

describe('ReportNote', () => {
  it('lists both notes', () => {
    expect(Object.values(ReportNote)).toEqual(['converted', 'recurring'])
  })

  it('exposes the named members as their literal values', () => {
    expect(ReportNote.Converted).toBe('converted')
    expect(ReportNote.Recurring).toBe('recurring')
  })
})
