/**
 * Unit tests for UiPreviewFrame: the sandbox and the other attributes are fixed, and nothing that would let the framed
 * page reach the panel or the top window is ever granted.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiPreviewFrame from '~/components/ui/UiPreviewFrame.vue'

describe('UiPreviewFrame', () => {
  it('writes exactly the agreed attributes', () => {
    const frame = mountWithKit(UiPreviewFrame, { props: { src: 'https://a.example/', title: 'Preview', testid: 'frame' } })

    expect(frame.element.tagName).toBe('IFRAME')
    expect(frame.attributes('src')).toBe('https://a.example/')
    expect(frame.attributes('title')).toBe('Preview')
    expect(frame.attributes('sandbox')).toBe('allow-scripts allow-forms')
    expect(frame.attributes('referrerpolicy')).toBe('no-referrer')
    expect(frame.attributes('allow')).toBe('')
    expect(frame.attributes('loading')).toBe('lazy')
    expect(frame.attributes('data-testid')).toBe('frame')
  })
})
