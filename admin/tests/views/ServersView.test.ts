/**
 * Mount tests for ServersView: servers grouped by module, the groups table, the connection test
 * result in the row, the delete confirmation, the empty and refused states, and the SSH dialog.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ServersView from '@/views/ServersView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { serverDto, serverGroupDto, serverSshSettingsDto, serverSshSetupDto } from '../helpers/serverFixtures'

/**
 * Mounts the screen and waits for the first load.
 *
 * @returns The wrapper.
 */
const mountServers = async () => {
  const { wrapper } = await mountRouted(ServersView, '/servers', '/servers')
  await flushPromises()
  return wrapper
}

describe('ServersView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists servers under their module with state and usage', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([serverGroupDto()]),
    })
    const wrapper = await mountServers()

    const row = wrapper.get('[data-testid="server-row-1"]')
    expect(row.text()).toContain('host1')
    expect(row.text()).toContain('12 / 100')
    expect(wrapper.get('[data-testid="server-state-1"]').text()).toBe('Online')
    expect(wrapper.find('[data-testid="servers-table-Cwp7"]').exists()).toBe(true)
  })

  it('lists the groups with their fill type and servers', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([serverGroupDto()]),
    })
    const wrapper = await mountServers()

    const row = wrapper.get('[data-testid="server-group-row-3"]')
    expect(row.text()).toContain('Shared')
    expect(row.text()).toContain('host1')
  })

  it('shows the connection test result in the row', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
      'POST /api/admin/servers/1/test-connection': () =>
        json({ connected: true, accountsCount: 14, version: '7.1', errorMessage: null, testedAt: '2026-10-14T00:00:00Z' }),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-test-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-test-result-1"]').text()).toContain('14')
  })

  it('shows the API sentence when the connection test is refused', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
      'POST /api/admin/servers/1/test-connection': () => problem('Credentials were rejected.', 400),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-test-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="server-test-result-1"]').text()).toContain('Credentials were rejected.')
  })

  it('asks for confirmation before deleting a server', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
      'DELETE /api/admin/servers/1': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url, init]) => String(url) === '/api/admin/servers/1' && init?.method === 'DELETE')).toBe(true)
    expect(wrapper.get('[data-testid="servers-feedback"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a server delete is refused', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
      'DELETE /api/admin/servers/1': () => problem('The server has services.', 409),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="servers-feedback"]').text()).toBe('The server has services.')
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('does not delete when the confirmation is dismissed', async () => {
    const mock = stubRoutes({
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false)
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/servers': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json([serverDto()])
      },
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    expect(wrapper.get('[data-testid="servers-error"]').text()).toBe('Not allowed.')
    await wrapper.get('[data-testid="servers-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="servers-list"]').exists()).toBe(true)
  })

  it('shows the empty states when there are no servers or groups', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([]),
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    expect(wrapper.find('[data-testid="servers-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="server-groups-empty"]').exists()).toBe(true)
  })

  it('opens the server dialog from the add button', async () => {
    stubRoutes({
      'GET /api/admin/servers': () => json([]),
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="servers-add"]').trigger('click')

    expect(wrapper.find('[data-testid="server-form-name"]').exists()).toBe(true)
  })

  it('opens the SSH key management dialog for the server of the row', async () => {
    // The first key the request line starts with answers, so the SSH routes go before the list
    // route they extend; listed after it, the dialog would be filled from the server list.
    stubRoutes({
      'GET /api/admin/servers/1/ssh-settings': () => json(serverSshSettingsDto()),
      'GET /api/admin/servers/1/ssh-setup': () => json(serverSshSetupDto()),
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-ssh-1"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(true)
    await wrapper.get('[data-testid="server-ssh-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="servers-feedback"]').text()).toBe('')
  })

  it('keeps the SSH dialog open after a save, with the confirmation inside it', async () => {
    const settings = serverSshSettingsDto()
    // The SSH routes go before the list route they extend: the first matching key answers.
    stubRoutes({
      'GET /api/admin/servers/1/ssh-settings': () => json(settings),
      'PUT /api/admin/servers/1/ssh-settings': () => json(settings),
      'GET /api/admin/servers/1/ssh-setup': () => json(serverSshSetupDto()),
      'GET /api/admin/servers': () => json([serverDto()]),
      'GET /api/admin/server-groups': () => json([]),
    })
    const wrapper = await mountServers()

    await wrapper.get('[data-testid="server-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="server-ssh-1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="server-ssh-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="server-ssh-hostname"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="server-ssh-saved"]').text()).toBe('Saved')
    expect(wrapper.get('[data-testid="servers-feedback"]').text()).toBe('')
  })
})
