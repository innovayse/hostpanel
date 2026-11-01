/**
 * Unit tests for AppAccountMenu: the avatar, the opening panel, keyboard and sign-out.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import AppAccountMenu from '@/components/layout/AppAccountMenu.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the menu with a router so the settings link resolves.
 *
 * @param email - The address to show.
 * @returns The wrapper.
 */
const mountMenu = async (email = 'admin@example.test') => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: { template: '<div />' } }, { path: '/settings', component: { template: '<div />' } }],
  })
  await router.push('/')
  return mount(AppAccountMenu, {
    props: { email },
    attachTo: document.body,
    global: { plugins: [createTestI18n(), router] },
  })
}

describe('AppAccountMenu', () => {
  it('shows the first letter of the address in the avatar and keeps the panel closed', async () => {
    const wrapper = await mountMenu()

    expect(wrapper.get('[data-testid="topbar-account"]').text()).toBe('A')
    expect(wrapper.find('[data-testid="topbar-account-menu"]').exists()).toBe(false)
  })

  it('opens with the full address, a settings link and sign-out', async () => {
    const wrapper = await mountMenu('very.long.address.of.an.operator@example.test')

    await wrapper.get('[data-testid="topbar-account"]').trigger('click')

    expect(wrapper.get('[data-testid="topbar-account"]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-testid="topbar-account-email"]').text()).toBe('very.long.address.of.an.operator@example.test')
    expect(wrapper.get('[data-testid="topbar-account-email"]').classes()).toContain('break-all')
    expect(wrapper.get('[data-testid="topbar-settings"]').attributes('href')).toBe('/settings')
  })

  it('emits sign-out and closes', async () => {
    const wrapper = await mountMenu()

    await wrapper.get('[data-testid="topbar-account"]').trigger('click')
    await wrapper.get('[data-testid="topbar-sign-out"]').trigger('click')

    expect(wrapper.emitted('sign-out')).toHaveLength(1)
    expect(wrapper.find('[data-testid="topbar-account-menu"]').exists()).toBe(false)
  })

  it('closes on Escape and on a click outside', async () => {
    const wrapper = await mountMenu()

    await wrapper.get('[data-testid="topbar-account"]').trigger('click')
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await flushPromises()
    expect(wrapper.find('[data-testid="topbar-account-menu"]').exists()).toBe(false)

    await wrapper.get('[data-testid="topbar-account"]').trigger('click')
    document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await flushPromises()
    expect(wrapper.find('[data-testid="topbar-account-menu"]').exists()).toBe(false)
    wrapper.unmount()
  })
})
