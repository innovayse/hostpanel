/**
 * Mount tests for AccountCard: the initials, address and role of the signed-in account, the menu behind the
 * avatar (settings, sign-out), and the folded rail drawing the avatar alone.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import AccountCard from '@/components/layout/AccountCard.vue'
import { useAuthStore } from '@/stores/auth'
import { stubRoutes } from '../../helpers/routes'
import { mountScreen } from '../../helpers/mount'

/**
 * Mounts the card with a signed-in account.
 *
 * @param props - Props of the card.
 * @returns The wrapper.
 */
const mountCard = async (props: Record<string, unknown> = {}) => {
  stubRoutes({})
  const { wrapper } = await mountScreen(AccountCard, props, '/dashboard')
  useAuthStore().user = { email: 'ada@example.com', firstName: 'Ada', lastName: 'Lovelace', roles: ['Admin'] }
  await flushPromises()
  return wrapper
}

describe('AccountCard', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the initials, the name and the role, with the address as the tooltip', async () => {
    const wrapper = await mountCard()

    expect(wrapper.get('[data-testid="account-avatar"]').text()).toBe('AL')
    expect(wrapper.get('[data-testid="account-name"]').text()).toBe('Ada Lovelace')
    expect(wrapper.get('[data-testid="account-name"]').attributes('title')).toBe('ada@example.com')
    expect(wrapper.get('[data-testid="account-role"]').text()).toBe('Admin')
  })

  it('falls back to the address when the account holds no name', async () => {
    const wrapper = await mountCard()
    useAuthStore().user = { email: 'ada@example.com', firstName: '', lastName: '', roles: ['Admin'] }
    await flushPromises()

    expect(wrapper.get('[data-testid="account-avatar"]').text()).toBe('AD')
    expect(wrapper.get('[data-testid="account-name"]').text()).toBe('ada@example.com')
  })

  it('opens the settings and sign-out actions from the avatar', async () => {
    const wrapper = await mountCard()
    expect(wrapper.get('[data-testid="sidebar-user-menu"]').attributes('aria-label')).toBe('Account menu')
    expect(wrapper.find('[data-testid="sidebar-settings"]').exists()).toBe(false)

    await wrapper.get('[data-testid="sidebar-user-menu"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="sidebar-settings"]').attributes('href')).toBe('/settings')
    expect(wrapper.find('[data-testid="sidebar-sign-out"]').exists()).toBe(true)
  })

  it('draws only the avatar in the folded rail', async () => {
    const wrapper = await mountCard({ rail: true })

    expect(wrapper.find('[data-testid="account-avatar"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="account-role"]').exists()).toBe(false)
  })

  it('sizes the avatar to fit inside the rail tile, and larger when the card is open', async () => {
    const rail = await mountCard({ rail: true })
    const open = await mountCard()

    expect(rail.get('[data-testid="account-avatar"]').classes()).toContain('size-8')
    expect(rail.get('[data-testid="account-card"]').classes()).toContain('p-0.5')
    expect(open.get('[data-testid="account-avatar"]').classes()).toContain('size-9')
  })
})
