/**
 * Tests for PluginTileSkeleton: it has the shape of a tile (picture, lines, footer under a divider),
 * is hidden from assistive technology and carries no text.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginTileSkeleton from '@/components/plugins/PluginTileSkeleton.vue'

describe('PluginTileSkeleton', () => {
  it('draws a card-shaped placeholder that stretches to the row', () => {
    const wrapper = mount(PluginTileSkeleton)

    expect(wrapper.attributes('data-testid')).toBe('plugin-tile-skeleton')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-full', 'flex', 'flex-col']))
  })

  it('draws a circle for the picture and pulsing lines for the text', () => {
    const wrapper = mount(PluginTileSkeleton)

    expect(wrapper.find('.rounded-full').exists()).toBe(true)
    expect(wrapper.findAll('.animate-pulse').length).toBeGreaterThanOrEqual(6)
  })

  it('pins a footer with a divider to the bottom', () => {
    const footer = mount(PluginTileSkeleton).find('.mt-auto')

    expect(footer.exists()).toBe(true)
    expect(footer.classes()).toContain('border-t')
  })

  it('shows no text and hides its placeholders from assistive technology', () => {
    const wrapper = mount(PluginTileSkeleton)

    expect(wrapper.text()).toBe('')
    expect(wrapper.findAll('[aria-hidden="true"]').length).toBeGreaterThan(0)
  })
})
