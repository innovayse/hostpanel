/**
 * Unit tests for UiAccent: the colour becomes the `--accent` property of a layout-free wrapper, and
 * only a plain hex is allowed through.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiAccent from '~/components/ui/UiAccent.vue'
import { UiAccentTag } from '~/utils/ui/enums'

describe('UiAccent', () => {
  it('writes a hex colour as the accent property and renders its slot', () => {
    const wrapper = mountWithKit(UiAccent, { props: { color: '#0ea5e9' }, slots: { default: 'inside' } })

    expect((wrapper.element as HTMLElement).style.getPropertyValue('--accent')).toBe('#0ea5e9')
    expect(wrapper.text()).toBe('inside')
  })

  it('writes the readable ink colour next to the accent', () => {
    const wrapper = mountWithKit(UiAccent, { props: { color: '#fde047' } })

    expect((wrapper.element as HTMLElement).style.getPropertyValue('--accent-ink')).toBe('#08090f')
  })

  it('takes no part in the layout', () => {
    expect(mountWithKit(UiAccent, { props: { color: '#fff' } }).classes()).toContain('contents')
  })

  it('refuses a colour that is not a plain hex', () => {
    const wrapper = mountWithKit(UiAccent, { props: { color: 'url(javascript:alert(1))' } })

    expect((wrapper.element as HTMLElement).style.getPropertyValue('--accent')).toBe('')
  })

  it('writes nothing when there is no colour', () => {
    const wrapper = mountWithKit(UiAccent)

    expect(wrapper.attributes('style')).toBeUndefined()
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiAccent, { props: { as: UiAccentTag.Li, color: '#fff' } }).element.tagName).toBe('LI')
  })
})
