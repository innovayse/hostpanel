/**
 * Tests for the client-side CSV download of the reports.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildCsv, downloadCsv } from '@/lib/reportsCsv'
import { captureDownloads } from '../helpers/captureDownloads'

describe('buildCsv', () => {
  it('quotes every field and joins lines with newlines', () => {
    expect(buildCsv(['Name', 'Amount'], [['Acme', 5], ['Beta', 6]])).toBe('"Name","Amount"\n"Acme","5"\n"Beta","6"')
  })

  it('doubles a quote inside a field', () => {
    expect(buildCsv(['Note'], [['say "hi"']])).toBe('"Note"\n"say ""hi"""')
  })
})

describe('downloadCsv', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('offers the rows as a named file and frees the object address', () => {
    const saved = captureDownloads()

    downloadCsv('top.csv', ['A'], [[1]])

    expect(saved).toHaveLength(1)
    expect(saved[0]?.fileName).toBe('top.csv')
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:download-0')
  })
})
