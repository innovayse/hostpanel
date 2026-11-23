/**
 * Mount tests for ServersView: the paged server list with its search, module and state filters sent to the API and kept
 * in the address, the groups table, the connection test result in the row, the delete confirmation, the empty and refused
 * states, and the SSH dialog.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ServersView from '@/views/ServersView.vue'
import { SERVER_GROUPS_CARD, SERVER_GROUPS_TABLE } from '@/views/ServersView.schema'
import { mountRouted } from '../helpers/mount'
import { applyListFilter } from '../helpers/listFilter'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { serverDto, serverGroupDto, serverModuleOptionDtos, serverOptionDto, serverSshSettingsDto, serverSshSetupDto } from '../helpers/serverFixtures'
import { useToastStore } from '@/stores/toast'
import { toastMessages } from '../helpers/toasts'

/** The address the screen is registered under. */
const PATH = '/servers'

/**
 * Wraps rows in the page shape the list endpoint answers with.
 *
 * @param items - The rows of the page.
 * @param totalCount - Matches across all pages; the page length by default.
 * @returns The JSON body.
 */
const page = (items: unknown[], totalCount = items.length) => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 20)),
})

/**
 * Mounts the screen and waits for the first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountServers = async (url: string = PATH) => {
  const screen = await mountRouted(ServersView, PATH, url)
  await flushPromises()
  return screen
}

/**
 * Lists the page requests the mock has seen.
 *
 * @param mock - The stubbed fetch.
 * @returns The request URLs that went to the paged list endpoint.
 */
const listUrls = (mock: ReturnType<typeof vi.fn>): string[] =>
  mock.mock.calls.map(([url]) => url as string).filter((url) => url.startsWith('/api/admin/servers?'))

describe('ServersView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists servers under their module with state and usage', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([serverGroupDto()]),
    })
    const { wrapper } = await mountServers()

    const row = wrapper.get('[data-testid="server-row-1"]')
    expect(row.text()).toContain('host1')
    expect(row.text()).toContain('12 / 100')
    expect(wrapper.get('[data-testid="server-state-1"]').text()).toBe('Online')
    expect(wrapper.get('[data-testid="servers-module-CPanel"]').text()).not.toBe('')
    expect(wrapper.get('progress').attributes('value')).toBe('12')
  })

  it('restores the search, the module, the state and the page from the address and sends them together', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto({ id: 2, name: 'host2', module: 'hestiacp', isOnline: false })], 45)),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers(`${PATH}?search=host&module=hestiacp&state=offline&page=2`)

    expect(listUrls(mock)[0]).toBe('/api/admin/servers?page=2&pageSize=20&search=host&module=hestiacp&state=offline')
    expect(wrapper.find('[data-testid="server-row-2"]').exists()).toBe(true)
    expect((wrapper.get('[data-testid="servers-search"]').element as HTMLInputElement).value).toBe('host')
    expect(wrapper.get('[data-testid="filter-chip-module"]').text()).toContain('hestiacp')
    expect(wrapper.get('[data-testid="filter-chip-state"]').text()).toContain('Offline')
    expect(wrapper.get('[data-testid="filter-trigger-count"]').text()).toBe('2')
  })

  it('does not send a module or a state the address holds that is not a known value', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    await mountServers(`${PATH}?module=Nonsense&state=sideways`)

    expect(listUrls(mock)[0]).toBe('/api/admin/servers?page=1&pageSize=20')
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper, router } = await mountServers()

    await wrapper.get('[data-testid="servers-search"]').setValue('host')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'host' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=1&pageSize=20&search=host')
  })

  it('sends the module and state picked in the filter dialog on Apply and shows them as chips', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper, router } = await mountServers()

    await applyListFilter(wrapper, { 'servers-filter-module': 'hestiacp', 'servers-filter-state': 'untested' })

    expect(router.currentRoute.value.query).toEqual({ module: 'hestiacp', state: 'untested' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=1&pageSize=20&module=hestiacp&state=untested')
    expect(wrapper.get('[data-testid="filter-chip-module"]').text()).toContain('hestiacp')
    expect(wrapper.get('[data-testid="filter-chip-state"]').text()).toContain('Not tested')
  })

  it('offers the modules plugins declare in the filter and sends the id as typed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto({ module: 'hestiacp' })])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper, router } = await mountServers()

    await applyListFilter(wrapper, { 'servers-filter-module': 'hestiacp' })

    expect(router.currentRoute.value.query).toEqual({ module: 'hestiacp' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=1&pageSize=20&module=hestiacp')
    expect(wrapper.get('[data-testid="filter-chip-module"]').text()).toContain('hestiacp')
    expect(wrapper.get('[data-testid="servers-module-hestiacp"]').text()).toBe('hestiacp')
  })

  it('removes the state from the address and reloads when its chip is removed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper, router } = await mountServers(`${PATH}?state=offline&module=hestiacp`)

    await wrapper.get('[data-testid="filter-chip-remove-state"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ module: 'hestiacp' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=1&pageSize=20&module=hestiacp')
  })

  it('keeps the chosen page in the address and reads it', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()], 45)),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper, router } = await mountServers()

    await wrapper.get('[data-testid="servers-pagination-next"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '2' })
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=2&pageSize=20')
  })

  it('says so when no server matches the filters, with the filters still in reach', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers(`${PATH}?module=hestiacp`)

    expect(wrapper.find('[data-testid="servers-no-matches"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="servers-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="servers-search"]').exists()).toBe(true)
  })

  it('lists the groups with their fill type and servers', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([serverGroupDto()]),
    })
    const { wrapper } = await mountServers()

    const row = wrapper.get('[data-testid="server-group-row-3"]')
    expect(row.text()).toContain('Shared')
    expect(row.text()).toContain('host1')
  })

  it('heads the groups table with the columns of its schema, inside the groups card', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([serverGroupDto()]),
    })
    const { wrapper } = await mountServers()

    const card = wrapper.get(`[data-testid="ui-detail-card-${SERVER_GROUPS_CARD}"]`)
    expect(card.findAll('[role="columnheader"]')).toHaveLength(SERVER_GROUPS_TABLE.columns.length)
    expect(card.find('[data-testid="server-groups-table"]').exists()).toBe(true)
  })

  it('shows the connection test result in the row and reads the page again for the new state', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
      'POST /api/admin/servers/1/test-connection': () =>
        json({ connected: true, accountsCount: 14, version: '7.1', errorMessage: null, testedAt: '2026-10-14T00:00:00Z' }),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-test-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-test-result-1"]').text()).toContain('14')
    expect(listUrls(mock)).toHaveLength(2)
  })

  it('shows the API sentence when the connection test is refused', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
      'POST /api/admin/servers/1/test-connection': () => problem('Credentials were rejected.', 400),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-test-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-test-result-1"]').text()).toContain('Credentials were rejected.')
  })

  it('asks for confirmation before deleting a server, then reads the page again', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
      'DELETE /api/admin/servers/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/servers/1' && init?.method === 'DELETE')).toBe(true)
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Record deleted.'])
    expect(listUrls(mock)).toHaveLength(2)
  })

  it('steps back a page when the delete empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(deleted ? page([], 20) : page([serverDto()], 21)),
      'GET /api/admin/server-groups': () => json([]),
      'DELETE /api/admin/servers/1': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper, router } = await mountServers(`${PATH}?page=2`)

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query.page).toBeUndefined()
    expect(listUrls(mock).at(-1)).toBe('/api/admin/servers?page=1&pageSize=20')
  })

  it('shows the API sentence when a server delete is refused', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
      'DELETE /api/admin/servers/1': () => problem('The server has services.', 409),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toContain('The server has services.')
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is dismissed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json(page([serverDto()]))
      },
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    expect(wrapper.get('[data-testid="servers-error"]').text()).toBe('Not allowed.')
    await wrapper.get('[data-testid="servers-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="servers-list"]').exists()).toBe(true)
  })

  it('shows the empty states when there are no servers or groups', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    expect(wrapper.find('[data-testid="servers-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="servers-no-matches"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="server-groups-empty"]').exists()).toBe(true)
  })

  it('opens the server dialog from the add button', async () => {
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers?': () => json(page([])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="servers-add"]').trigger('click')

    expect(wrapper.find('[data-testid="server-form-name"]').exists()).toBe(true)
  })

  it('offers every server, not the page, in the group dialog', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers/options': () => json([serverOptionDto({ id: 1, name: 'host1' }), serverOptionDto({ id: 9, name: 'on-another-page' })]),
      'GET /api/admin/servers?': () => json(page([serverDto()], 21)),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="servers-add-group"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="group-form-add-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="group-form-add-9"]').exists()).toBe(true)
    expect(mock.mock.calls.some(([url]) => url === '/api/admin/servers/options')).toBe(true)
  })

  it('reads the groups and the page again after a group is saved', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers/options': () => json([serverOptionDto()]),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
      'POST /api/admin/server-groups': () => json(5, 201),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="servers-add-group"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="group-form-name"]').setValue('Shared')
    await wrapper.get('[data-testid="group-form-save"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => url === '/api/admin/server-groups' && init?.method === 'POST')).toBe(true)
    expect(listUrls(mock)).toHaveLength(2)
    expect(mock.mock.calls.filter(([url, init]) => url === '/api/admin/server-groups' && (init?.method ?? 'GET') === 'GET')).toHaveLength(2)
  })

  it('opens the SSH key management dialog for the server of the row', async () => {
    // The first key the request line starts with answers, so the SSH routes go before the list
    // route they extend; listed after it, the dialog would be filled from the server list.
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers/1/ssh-settings': () => json(serverSshSettingsDto()),
      'GET /api/admin/servers/1/ssh-setup': () => json(serverSshSetupDto()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-ssh-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(true)
    await wrapper.get('[data-testid="server-ssh-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(false)
    expect(toastMessages()).toEqual([])
  })

  it('keeps the SSH dialog open after a save, with the confirmation inside it', async () => {
    const settings = serverSshSettingsDto()
    // The SSH routes go before the list route they extend: the first matching key answers.
    stubRoutes({
      'GET /api/admin/servers/modules': () => json(serverModuleOptionDtos()),
      'GET /api/admin/servers/1/ssh-settings': () => json(settings),
      'PUT /api/admin/servers/1/ssh-settings': () => json(settings),
      'GET /api/admin/servers/1/ssh-setup': () => json(serverSshSetupDto()),
      'GET /api/admin/servers?': () => json(page([serverDto()])),
      'GET /api/admin/server-groups': () => json([]),
    })
    const { wrapper } = await mountServers()

    await wrapper.get('[data-testid="server-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="server-ssh-1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="server-ssh-saved"]').text()).toBe('Saved')
    expect(toastMessages()).toEqual([])
  })

  it('keeps the page header, the toolbar and the card grid and draws placeholder cards while the servers load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountServers()

    expect(wrapper.get('[data-testid="servers-list"]').findAll('[data-testid="server-card-loading"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="servers-search"]').exists()).toBe(true)
    expect(wrapper.find(`[data-testid="ui-detail-card-${SERVER_GROUPS_CARD}"] [data-testid="ui-table-skeleton"]`).exists()).toBe(true)
    expect(wrapper.find('[data-testid="servers-empty"]').exists()).toBe(false)
  })
})
