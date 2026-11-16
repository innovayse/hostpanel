/**
 * Unit tests for PluginCardSkeleton: the placeholder keeps the card shape and is hidden from assistive technology.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginCardSkeleton from '@/components/plugins/PluginCardSkeleton.vue'

describe('PluginCardSkeleton', () => {
  it('draws bars for the logo, name, badges, description lines and footer', () => {
    const wrapper = mount(PluginCardSkeleton)

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(8)
  })

  it('is hidden from assistive technology', () => {
    const wrapper = mount(PluginCardSkeleton)

    expect(wrapper.get('[data-testid="plugin-card-skeleton"]').attributes('aria-hidden')).toBe('true')
  })
})
