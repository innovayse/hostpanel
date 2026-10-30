/**
 * Tests for `utils/saveBlob.ts`: the blob is handed to the browser as a download under the given
 * name, and its object URL is released afterwards.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { saveBlob } from '@/utils/saveBlob'

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('saveBlob', () => {
  it('clicks a download link for the blob and then releases its URL', () => {
    const createObjectURL = vi.fn(() => 'blob:invoice')
    const revokeObjectURL = vi.fn()
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL, revokeObjectURL }))
    const clicked: HTMLAnchorElement[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push(this)
    })
    const pdf = new Blob(['%PDF'], { type: 'application/pdf' })

    saveBlob(pdf, 'invoice-41.pdf')

    expect(createObjectURL).toHaveBeenCalledWith(pdf)
    expect(clicked).toHaveLength(1)
    expect(clicked[0]?.href).toBe('blob:invoice')
    expect(clicked[0]?.download).toBe('invoice-41.pdf')
    expect(clicked[0]?.isConnected).toBe(false)
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:invoice')
  })
})
