/**
 * Mount tests for ClientLayout: it renders against the real kit, settles on an API refusal, and for a loaded
 * client shows the glass header card (avatar, status, key figures) with the section tabs under it.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import ClientLayout from '@/layouts/ClientLayout.vue'
import { mountClientView } from '../helpers/clientViews'
import { createTestI18n } from '../helpers/i18n'
import { stubPathRoutes } from '../helpers/routes'

/** A client, as the detail endpoint answers it. */
const CLIENT = {
  id: 5,
  userId: 'u-5',
  email: 'ada@example.test',
  firstName: 'Ada',
  lastName: 'Lovelace',
  companyName: 'Analytical Engines',
  status: 'Active',
  isUserDeleted: false,
  twoFactorEnabled: true,
  createdAt: '2026-01-01T00:00:00Z',
  clientGroupId: 4,
  clientGroupName: 'VIP',
  clientGroupColor: '#112233',
  address: {},
  preferences: { currency: 'EUR' },
  notifications: {},
  settings: {},
  contacts: [],
  creditBalance: 25,
}

/**
 * Mounts the layout over a loaded client.
 *
 * @returns The wrapper after the client has loaded.
 */
const mountLoaded = async () => {
  stubPathRoutes([{ path: '/api/clients/5', body: CLIENT }, { path: '/api/admin/currencies', body: [] }])
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/clients/:id/:section', component: { render: () => null } }],
  })
  await router.push('/clients/5/summary')
  await router.isReady()
  const wrapper = mount(ClientLayout, {
    global: { plugins: [createTestI18n(), createPinia(), router], stubs: { teleport: true } },
  })
  await flushPromises()
  return wrapper
}

describe('ClientLayout', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the header card, the figure captions and the tabs with bars while the client loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/clients/:id/:section', component: { render: () => null } }],
    })
    await router.push('/clients/5/summary')
    await router.isReady()
    const wrapper = mount(ClientLayout, {
      global: { plugins: [createTestI18n(), createPinia(), router], stubs: { teleport: true } },
    })
    await flushPromises()

    expect(wrapper.get('[data-testid="client-layout-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="client-layout-stats"]').text()).toContain('ID')
    expect(wrapper.find('[data-testid="client-layout-export"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="client-header-name"]').exists()).toBe(false)
  })

  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientLayout)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientLayout, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('shows the avatar, the name, the status and the group in the header card', async () => {
    const wrapper = await mountLoaded()

    const header = wrapper.get('[data-testid="client-layout-header"]')
    expect(header.get('[data-testid="client-layout-avatar"]').text()).toBe('AL')
    expect(wrapper.get('[data-testid="client-header-name"]').text()).toBe('Ada Lovelace')
    expect(header.text()).toContain('Active')
    expect(header.text()).toContain('VIP')
    expect(header.text()).toContain('Analytical Engines')
  })

  it('shows the key figures of the client', async () => {
    const wrapper = await mountLoaded()

    expect(wrapper.get('[data-testid="client-layout-stat-id"]').text()).toContain('5')
    expect(wrapper.get('[data-testid="client-layout-stat-email"]').text()).toContain('ada@example.test')
    expect(wrapper.get('[data-testid="client-layout-stat-two-factor"]').text()).toContain('On')
    expect(wrapper.get('[data-testid="client-layout-stat-credit"]').text()).toContain('25.00 EUR')
  })

  it('shows the section tabs with the current section selected', async () => {
    const wrapper = await mountLoaded()

    expect(wrapper.findAll('[data-testid="client-nav-strip"] [role="tab"]')).toHaveLength(14)
    expect(wrapper.get('[data-testid="client-tab-summary"]').attributes('aria-selected')).toBe('true')
  })
})
