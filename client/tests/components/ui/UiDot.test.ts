/**
 * Unit tests for UiDot: a decorative round dot whose size comes from the size prop and whose
 * colour comes from the caller's class.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiDot from '~/components/ui/UiDot.vue'
import { UiDotSize } from '~/utils/ui/enums'

describe('UiDot', () => {
  it('is a round, decorative element of the default size', () => {
    const wrapper = mountWithKit(UiDot)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['rounded-full', 'h-2', 'w-2']))
    expect(wrapper.attributes('aria-hidden')).toBe('true')
  })

  it('takes its size from the prop and its colour from the caller', () => {
    const wrapper = mountWithKit(UiDot, { props: { size: UiDotSize.Lg }, attrs: { class: 'bg-ok' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-[9px]', 'w-[9px]', 'bg-ok']))
  })
})
