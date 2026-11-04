/**
 * Unit tests for UiPluginAvatar: the decorative icon with contain fit, the colour block with the
 * initial when there is no icon, the fallback when the icon fails to load, and a retry on a new address.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import { DARK_TEXT_COLOR, LIGHT_TEXT_COLOR, NEUTRAL_COLOR } from '@/utils/ui/color'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the avatar.
 *
 * @param props - Props to set.
 * @returns The wrapper.
 */
const mountAvatar = (props: Record<string, unknown> = {}) =>
  mount(UiPluginAvatar, { props: { name: 'Acme Pay', ...props }, global: { plugins: [createTestI18n()] } })

describe('UiPluginAvatar', () => {
  it('shows the icon as a decorative image with a contain fit', () => {
    const wrapper = mountAvatar({ iconUrl: '/api/admin/plugins/acme/icon', color: '#123456' })
    const image = wrapper.get('[data-testid="plugin-avatar-image"]')

    expect(image.attributes('src')).toBe('/api/admin/plugins/acme/icon')
    expect(image.attributes('alt')).toBe('')
    expect(image.attributes('aria-hidden')).toBe('true')
    expect(image.classes()).toContain('object-contain')
    expect(wrapper.find('[data-testid="plugin-avatar-fallback"]').exists()).toBe(false)
  })

  it('shows the colour block with the initial when there is no icon', () => {
    const wrapper = mountAvatar({ iconUrl: null, color: '#123456' })

    expect(wrapper.find('[data-testid="plugin-avatar-image"]').exists()).toBe(false)
    expect(wrapper.get('rect').attributes('fill')).toBe('#123456')
    expect(wrapper.get('text').text()).toBe('A')
    expect(wrapper.get('svg').attributes('aria-hidden')).toBe('true')
    expect(wrapper.get('text').attributes('fill')).toBe(LIGHT_TEXT_COLOR)
  })

  it('uses dark letter colour on a light block', () => {
    const wrapper = mountAvatar({ iconUrl: null, color: '#facc15' })

    expect(wrapper.get('text').attributes('fill')).toBe(DARK_TEXT_COLOR)
  })

  it('falls back to the colour block when the icon fails to load', async () => {
    const wrapper = mountAvatar({ iconUrl: '/broken.png', color: '#123456' })

    await wrapper.get('[data-testid="plugin-avatar-image"]').trigger('error')

    expect(wrapper.find('[data-testid="plugin-avatar-image"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-avatar-fallback"]').exists()).toBe(true)
  })

  it('tries again when the address changes', async () => {
    const wrapper = mountAvatar({ iconUrl: '/broken.png' })

    await wrapper.get('[data-testid="plugin-avatar-image"]').trigger('error')
    await wrapper.setProps({ iconUrl: '/fixed.png' })

    expect(wrapper.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/fixed.png')
  })

  it('uses a neutral colour and a question mark for an empty name without a colour', () => {
    const wrapper = mountAvatar({ name: ' ' })

    expect(wrapper.get('rect').attributes('fill')).toBe(NEUTRAL_COLOR)
    expect(wrapper.get('text').text()).toBe('?')
  })
})
