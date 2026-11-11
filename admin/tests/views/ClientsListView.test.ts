/**
 * Mount tests for ClientsListView: it renders against the real kit, settles on an API refusal,
 * shows each client's group and filters the list by group.
 */
import { describe, expect, it, vi } from 'vitest'
import ClientsListView from '@/views/ClientsListView.vue'
import { mountClientView } from '../helpers/clientViews'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createTestI18n } from '../helpers/i18n'
import { stubPathRoutes } from '../helpers/routes'

/** A client in a group, as the list endpoint answers it. */
const GROUPED_CLIENT = {
  id: 8,
  userId: 'u-8',
  email: 'ada@example.test',
  firstName: 'Ada',
  lastName: 'Lovelace',
  status: 'Active',
  isUserDeleted: false,
  twoFactorEnabled: false,
  createdAt: '2026-01-01T00:00:00Z',
  clientGroupId: 4,
  clientGroupName: 'VIP',
  clientGroupColor: '#112233',
}

/** The group of that client, as the admin list answers it. */
const GROUP = { id: 4, name: 'VIP', description: null, discountPercent: 10, color: '#112233', isDefault: false, clientCount: 1 }

/**
 * Mounts the list over a client in a group.
 *
 * @returns The wrapper and the fetch mock.
 */
const mountGrouped = async () => {
  const fetchMock = stubPathRoutes([
    { path: '/api/clients', body: { items: [GROUPED_CLIENT], totalCount: 1 } },
    { path: '/api/admin/client-groups', body: [GROUP] },
  ])
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] })
  await router.push('/clients')
  await router.isReady()
  const wrapper = mount(ClientsListView, { global: { plugins: [createTestI18n(), createPinia(), router], stubs: { teleport: true } } })
  await flushPromises()
  return { wrapper, fetchMock }
}

describe('ClientsListView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientsListView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders its main action', async () => {
    const wrapper = await mountClientView(ClientsListView)

    expect(wrapper.find('[data-testid="clients-list-search"]').exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientsListView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('loads again from the Retry of a refused load', async () => {
    const wrapper = await mountClientView(ClientsListView)
    await flushPromises()
    const fetchMock = vi.mocked(fetch)
    const before = fetchMock.mock.calls.length

    await wrapper.get('[data-testid="clients-list-error-retry"]').trigger('click')
    await flushPromises()

    expect(fetchMock.mock.calls.length).toBeGreaterThan(before)
  })

  it('shows the group of a client as a coloured badge', async () => {
    const { wrapper } = await mountGrouped()

    const row = wrapper.get('[data-testid="client-row"]')
    expect(row.text()).toContain('VIP')
    expect(row.get('circle').attributes('fill')).toBe('#112233')
  })

  it('sends the picked group as the clientGroupId filter', async () => {
    const { wrapper, fetchMock } = await mountGrouped()
    wrapper.findAllComponents(UiSelect).find(c => c.attributes('data-testid') === 'clients-list-filter-group')?.vm.$emit('update:modelValue', 4)
    await flushPromises()

    await wrapper.get('[data-testid="clients-list-search"]').trigger('click')
    await flushPromises()

    const urls = fetchMock.mock.calls.map(call => String(call[0]))
    expect(urls.some(url => url.startsWith('/api/clients?') && url.includes('clientGroupId=4'))).toBe(true)
  })
})
