/**
 * Unit tests for UiLinkCard: the link target, the content and the highlighted frame.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiLinkCard from '~/components/ui/UiLinkCard.vue'
import { clientServicePath } from '~/constants/clientRoutes'

describe('UiLinkCard', () => {
  it('is one link around its content', () => {
    const wrapper = mountWithKit(UiLinkCard, { props: { to: clientServicePath(4), testid: 'card' }, slots: { default: 'Plan' } })

    expect(wrapper.element.tagName).toBe('A')
    expect(wrapper.attributes('href')).toBe(clientServicePath(4))
    expect(wrapper.attributes('data-testid')).toBe('card')
    expect(wrapper.text()).toBe('Plan')
  })

  it('carries the group class so children can react to hover', () => {
    const wrapper = mountWithKit(UiLinkCard, { props: { to: '/x' } })

    expect(wrapper.classes()).toContain('group')
  })

  it('draws the frame in blue when highlighted', () => {
    const plain = mountWithKit(UiLinkCard, { props: { to: '/x' } })
    const highlighted = mountWithKit(UiLinkCard, { props: { to: '/x', highlight: true } })

    expect(plain.classes()).not.toContain('border-blue-500/20')
    expect(highlighted.classes()).toContain('border-blue-500/20')
  })

  it('draws a keyboard focus ring', () => {
    const wrapper = mountWithKit(UiLinkCard, { props: { to: '/x' } })

    expect(wrapper.classes()).toContain('focus-visible:ring-2')
  })
})
