/**
 * Mount tests for ManageUsersView: rows render with their actions, the search is sent to the API,
 * the password-reset action confirms with a success message, deleting a user is confirmed first and
 * reports its outcome, and a refused load shows the API's sentence with a Retry instead of the table.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ManageUsersView from '@/views/ManageUsersView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'

/** One user as the list endpoint returns it. */
const user = {
  id: 'u1',
  clientId: 7,
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  language: null,
  lastLoginAt: null,
  createdAt: '2026-01-01T00:00:00Z',
}

/**
 * Mounts the screen and waits for the first load.
 *
 * @returns The wrapper.
 */
const mountUsers = async () => {
  const { wrapper } = await mountRouted(ManageUsersView, '/clients/users', '/clients/users')
  await flushPromises()
  return wrapper
}

describe('ManageUsersView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders a row per user with the three actions', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    const row = wrapper.get('[data-testid="users-row-u1"]')
    expect(row.text()).toContain('Ada')
    expect(row.text()).toContain('ada@example.com')
    expect(row.text()).toContain('Never')
    await wrapper.get('[data-testid="users-row-u1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="users-manage-u1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="users-reset-u1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="users-password-u1"]').exists()).toBe(true)
  })

  it('sends the typed search to the API', async () => {
    const mock = stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="users-search"]').setValue('ada')
    await wrapper.get('[data-testid="users-search-submit"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([url]) => String(url).includes('search=ada'))).toBe(true)
  })

  it('confirms a sent password reset email', async () => {
    stubRoutes({
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
      'POST /api/admin/users/u1/reset-password': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="users-row-u1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="users-reset-u1"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="users-action-success"]').text()).toContain('Password reset email sent.')
  })

  it('shows the API sentence instead of the table when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/users': () => problem('Users are unavailable.', 503) })
    const wrapper = await mountUsers()

    expect(wrapper.get('[data-testid="users-error"]').text()).toContain('Users are unavailable.')
    expect(wrapper.find('[data-testid="users-table"]').exists()).toBe(false)
  })

  it('shows the empty state when there are no users', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [], totalCount: 0 }) })
    const wrapper = await mountUsers()

    expect(wrapper.text()).toContain('No users found')
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({
      'GET /api/admin/users': () => (refused ? problem('Users are unavailable.', 503) : json({ items: [user], totalCount: 1 })),
    })
    const wrapper = await mountUsers()

    refused = false
    await wrapper.get('[data-testid="users-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="users-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="users-row-u1"]').exists()).toBe(true)
  })

  /**
   * Opens the manage modal of the user and asks to delete them.
   *
   * @param wrapper - The mounted screen.
   * @returns Resolves when the confirmation prompt is open.
   */
  const askDelete = async (wrapper: Awaited<ReturnType<typeof mountUsers>>): Promise<void> => {
    await wrapper.get('[data-testid="users-row-u1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="users-manage-u1"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="clients-userform-delete"]').trigger('click')
  }

  it('deletes a user only after the confirmation and reports it', async () => {
    const mock = stubRoutes({
      'GET /api/admin/users/u1': () => json({ ...user, accounts: [] }),
      'DELETE /api/admin/users/u1': () => new Response(null, { status: 204 }),
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
    })
    const wrapper = await mountUsers()
    await askDelete(wrapper)

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(true)
    expect(wrapper.get('[data-testid="users-action-success"]').text()).toContain('User deleted.')
  })

  it('keeps the user when the confirmation is cancelled', async () => {
    const mock = stubRoutes({
      'GET /api/admin/users/u1': () => json({ ...user, accounts: [] }),
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
    })
    const wrapper = await mountUsers()
    await askDelete(wrapper)

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    expect(wrapper.find('[data-testid="users-action-success"]').exists()).toBe(false)
  })

  it('shows the API sentence when the delete is refused', async () => {
    stubRoutes({
      'GET /api/admin/users/u1': () => json({ ...user, accounts: [] }),
      'DELETE /api/admin/users/u1': () => problem('The user owns an account.', 409),
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
    })
    const wrapper = await mountUsers()
    await askDelete(wrapper)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="users-action-error"]').text()).toContain('The user owns an account.')
  })
})
