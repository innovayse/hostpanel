/**
 * Unit tests for UiDecor: it is a hidden, pointer-less, absolutely positioned layer that keeps the
 * classes it is given.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiDecor from '~/components/ui/UiDecor.vue'
import { UiDecorTag } from '~/utils/ui/enums'

describe('UiDecor', () => {
  it('is hidden from assistive technology and takes no pointer events', () => {
    const wrapper = mountWithKit(UiDecor)

    expect(wrapper.attributes('aria-hidden')).toBe('true')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['absolute', 'pointer-events-none']))
  })

  it('keeps the classes it is given and renders its slot', () => {
    const wrapper = mountWithKit(UiDecor, { attrs: { class: 'top-0 left-0 w-10' }, slots: { default: 'x' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['top-0', 'left-0', 'w-10']))
    expect(wrapper.text()).toBe('x')
  })

  it('renders the requested tag', () => {
    expect(mountWithKit(UiDecor, { props: { as: UiDecorTag.Span } }).element.tagName).toBe('SPAN')
  })
})
