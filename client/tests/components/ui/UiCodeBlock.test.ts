/**
 * Unit tests for UiCodeBlock: a scrolling dark block rendered as `code` by default and as `pre` on
 * request, carrying its slot and the caller's class.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiCodeBlock from '~/components/ui/UiCodeBlock.vue'
import { UiCodeBlockTag } from '~/utils/ui/enums'

describe('UiCodeBlock', () => {
  it('renders its slot in a scrolling code element', () => {
    const wrapper = mountWithKit(UiCodeBlock, { slots: { default: 'ssh-keygen' } })

    expect(wrapper.element.tagName).toBe('CODE')
    expect(wrapper.text()).toBe('ssh-keygen')
    expect(wrapper.classes()).toContain('overflow-x-auto')
  })

  it('renders a pre element when asked and keeps the caller class', () => {
    const wrapper = mountWithKit(UiCodeBlock, { props: { as: UiCodeBlockTag.Pre }, attrs: { class: 'flex-1' } })

    expect(wrapper.element.tagName).toBe('PRE')
    expect(wrapper.classes()).toContain('flex-1')
  })
})
