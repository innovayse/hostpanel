/**
 * Mount tests for ClientsListView: it renders against the real kit, settles on an API refusal,
 * shows each client's group, and keeps its filters, search and page in the address.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
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
const GROUP = {
  id: 4,
  name: 'VIP',
  description: null,
  discountPercent: 10,
  color: '#112233',
  isDefault: false,
  clientCount: 1,
}

/**
 * Mounts the list over a client in a group.
 *
 * @param path - The address the router starts at, with the query the list should restore.
 * @returns The wrapper, the router and the fetch mock.
 */
const mountGrouped = async (path = '/clients') => {
  const fetchMock = stubPathRoutes([
    { path: '/api/clients', body: { items: [GROUPED_CLIENT], totalCount: 45 } },
    { path: '/api/admin/client-groups', body: [GROUP] },
  ])
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }],
  })
  await router.push(path)
  await router.isReady()
  const wrapper = mount(ClientsListView, {
    global: { plugins: [createTestI18n(), createPinia(), router], stubs: { teleport: true } },
  })
  await flushPromises()
  return { wrapper, router, fetchMock }
}

/**
 * Lists the client list requests the mock has seen.
 *
 * @param fetchMock - The stubbed fetch.
 * @returns The request URLs that went to the list endpoint.
 */
const listUrls = (fetchMock: ReturnType<typeof vi.fn>): string[] =>
  fetchMock.mock.calls.map(call => String(call[0])).filter(url => url.startsWith('/api/clients?'))

describe('ClientsListView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientsListView)

    expect(wrapper.exists()).toBe(true)
  })

  it('renders the search box and the filters button', async () => {
    const wrapper = await mountClientView(ClientsListView)

    expect(wrapper.find('[data-testid="clients-list-filter-name"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(true)
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

  it('opens a client from its row action as a real link', async () => {
    const { wrapper } = await mountGrouped()

    expect(wrapper.get('[data-testid="client-row-open-8"]').attributes('href')).toBe('/clients/8')
  })

  it('restores the filters, the search and the page from the address and sends them', async () => {
    const { wrapper, fetchMock } = await mountGrouped('/clients?search=ada&status=Active&clientGroupId=4&page=2')

    const url = listUrls(fetchMock)[0] ?? ''
    expect(url).toContain('page=2')
    expect(url).toContain('search=ada')
    expect(url).toContain('status=Active')
    expect(url).toContain('clientGroupId=4')
    expect((wrapper.get('[data-testid="clients-list-filter-name"]').element as HTMLInputElement).value).toBe('ada')
  })

  it('shows a chip per applied filter, naming the group', async () => {
    const { wrapper } = await mountGrouped('/clients?status=Active&clientGroupId=4')

    expect(wrapper.get('[data-testid="filter-chip-status"]').text()).toContain('Active')
    expect(wrapper.get('[data-testid="filter-chip-clientGroupId"]').text()).toContain('VIP')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('removes a filter from the address and reloads when its chip is removed', async () => {
    const { wrapper, router, fetchMock } = await mountGrouped('/clients?status=Active&clientGroupId=4')
    const before = listUrls(fetchMock).length

    await wrapper.get('[data-testid="filter-chip-remove-status"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ clientGroupId: '4' })
    expect(listUrls(fetchMock).length).toBeGreaterThan(before)
    expect(listUrls(fetchMock).at(-1)).not.toContain('status=')
  })

  it('sends the group picked in the filter dialog as clientGroupId on Apply', async () => {
    const { wrapper, router, fetchMock } = await mountGrouped()

    await wrapper.get('[data-testid="filter-trigger"]').trigger('click')
    await flushPromises()
    const picker = wrapper
      .findAllComponents(UiSelect)
      .find(c => c.attributes('data-testid') === 'clients-list-filter-group')
    picker?.vm.$emit('update:modelValue', 4)
    await flushPromises()
    await wrapper.get('[data-testid="filter-apply"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.clientGroupId).toBe('4')
    expect(listUrls(fetchMock).at(-1)).toContain('clientGroupId=4')
  })

  it('loads the page the pager chooses', async () => {
    const { wrapper, router, fetchMock } = await mountGrouped()

    await wrapper.get('[data-testid="clients-list-pagination-page-2"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBe('2')
    expect(listUrls(fetchMock).at(-1)).toContain('page=2')
  })

  it('asks for the chosen page size and returns to the first page', async () => {
    const { wrapper, router, fetchMock } = await mountGrouped('/clients?page=2')

    const sizeSelect = wrapper
      .findAllComponents(UiSelect)
      .find(c => c.attributes('data-testid') === 'clients-list-pagination-page-size')
    sizeSelect?.vm.$emit('update:modelValue', 50)
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBeUndefined()
    expect(listUrls(fetchMock).at(-1)).toContain('pageSize=50')
    expect(listUrls(fetchMock).at(-1)).toContain('page=1')
  })
})

describe('ClientsListView loading and phone layout', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the toolbar and the table header while the first load is pending, with placeholder rows', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = mount(ClientsListView, {
      global: { plugins: [createTestI18n(), createPinia(), createRouter({ history: createMemoryHistory(), routes: [{ path: '/:pathMatch(.*)*', component: { render: () => null } }] })], stubs: { teleport: true } },
    })
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="clients-list-table"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="clients-list-pagination-range"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-row"]').exists()).toBe(false)
  })

  it('hides the secondary columns on a phone and shows the email under the name', async () => {
    const { wrapper } = await mountGrouped()

    const row = wrapper.get('[data-testid="client-row"]')
    const cells = row.findAll('td')

    expect(cells[0]?.classes()).toContain('hidden')
    expect(cells[1]?.classes()).not.toContain('hidden')
    expect(cells[1]?.text()).toContain('ada@example.test')
    expect(cells[2]?.classes()).toContain('hidden')
  })
})
