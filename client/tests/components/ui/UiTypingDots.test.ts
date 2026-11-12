/**
 * Unit tests for UiTypingDots: a status region named by its label with three dots that stand still for reduced motion.
 *
 * @module tests/components/ui/UiTypingDots.test
 */

import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiTypingDots from '~/components/ui/UiTypingDots.vue'

describe('UiTypingDots', () => {
  it('is a status region named by the label', () => {
    const wrapper = mountWithKit(UiTypingDots, { props: { label: 'Writing a reply' } })

    expect(wrapper.attributes('role')).toBe('status')
    expect(wrapper.attributes('aria-label')).toBe('Writing a reply')
  })

  it('draws three dots, each stopping for reduced motion', () => {
    const dots = mountWithKit(UiTypingDots, { props: { label: 'Writing' } }).findAll('span span')

    expect(dots).toHaveLength(3)
    for (const dot of dots) {
      expect(dot.classes()).toContain('motion-reduce:animate-none')
    }
  })
})
