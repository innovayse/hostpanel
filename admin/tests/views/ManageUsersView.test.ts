/**
 * Mount tests for ManageUsersView: rows render with their actions, the search and the page live in the address and
 * are sent to the API, the password actions confirm with a success toast and report a refusal as a toast, they are not offered when the sign-on service owns the passwords, deleting a user is confirmed first and
 * reports its outcome, and a refused load shows the API's sentence with a Retry instead of the table.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ManageUsersView from '@/views/ManageUsersView.vue'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'
import { json, problem } from '../helpers/fetch'
import { renderedCaptions, schemaCaptions, stubPendingFetch } from '../helpers/schemaTable'
import schema from '@/views/ManageUsersView.schema'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toast'
import { AuthMode } from '@/types/auth'
import { UiTone } from '@/utils/ui/tone'

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
 * Mounts the screen at an address and waits for the first load.
 *
 * @param url - The address to open, with its query.
 * @returns The wrapper and the router.
 */
const mountUsersAt = async (url: string) => {
  const screen = await mountRouted(ManageUsersView, '/clients/users', url)
  await flushPromises()
  return screen
}

/**
 * Mounts the screen at its plain address and waits for the first load.
 *
 * @returns The wrapper.
 */
const mountUsers = async () => (await mountUsersAt('/clients/users')).wrapper

/**
 * The URL of every call a stub received.
 *
 * @param mock - The mock returned by `stubRoutes`.
 * @returns One URL per call, in order.
 */
const urlsOf = (mock: ReturnType<typeof stubRoutes>): string[] => mock.mock.calls.map(([url]) => url as string)

/**
 * The toasts on screen as sentence and tone pairs.
 *
 * @returns One pair per toast, oldest first.
 */
const toastsOnScreen = (): Array<[string, UiTone]> => useToastStore().toasts.map((toast) => [toast.message, toast.tone])

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
    await wrapper.get('[data-testid="users-actions-u1"]').trigger('click')
    expect(wrapper.find('[data-testid="users-manage-u1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="users-reset-u1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="users-password-u1"]').exists()).toBe(true)
  })

  it('asks for the first page, with the page size, when the address names neither', async () => {
    const mock = stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    await mountUsers()

    expect(urlsOf(mock)).toEqual(['/api/admin/users?page=1&pageSize=20'])
  })

  it('puts the typed search in the address and sends it after the pause', async () => {
    const mock = stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const { wrapper, router } = await mountUsersAt('/clients/users')

    await wrapper.get('[data-testid="users-search"]').setValue('ada')
    await new Promise((resolve) => setTimeout(resolve, 450))
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'ada' })
    expect(urlsOf(mock).at(-1)).toBe('/api/admin/users?page=1&pageSize=20&search=ada')
  })

  it('restores the search from the address and sends it', async () => {
    const mock = stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const { wrapper } = await mountUsersAt('/clients/users?search=ada')

    expect(urlsOf(mock)[0]).toBe('/api/admin/users?page=1&pageSize=20&search=ada')
    expect((wrapper.get('[data-testid="users-search"]').element as HTMLInputElement).value).toBe('ada')
  })

  it('has no search button of its own: the search is sent as it is typed', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    expect(wrapper.find('[data-testid="users-search-submit"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="users-search-clear"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="filter-trigger"]').exists()).toBe(false)
  })

  it('opens the page the address names and moves to another through the address', async () => {
    const mock = stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 45 }) })
    const { wrapper, router } = await mountUsersAt('/clients/users?page=2&search=ada')

    expect(urlsOf(mock)[0]).toBe('/api/admin/users?page=2&pageSize=20&search=ada')
    await wrapper.get('[data-testid="users-pagination-page-3"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ page: '3', search: 'ada' })
    expect(urlsOf(mock).at(-1)).toBe('/api/admin/users?page=3&pageSize=20&search=ada')
  })

  it('offers no header action: new clients are added from the top bar, not from the users page', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    expect(wrapper.find('[data-testid="users-add-client"]').exists()).toBe(false)
  })

  it('draws the pager in the table card', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 45 }) })
    const wrapper = await mountUsers()

    expect(wrapper.get('[data-testid="users-table"]').element.closest('[data-testid="users-pagination"]')).toBeNull()
    expect(wrapper.find('[data-testid="users-pagination-page-3"]').exists()).toBe(true)
  })

  it('confirms a sent password reset email', async () => {
    stubRoutes({
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
      'POST /api/admin/users/u1/reset-password': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="users-actions-u1"]').trigger('click')
    await wrapper.get('[data-testid="users-reset-u1"]').trigger('click')
    await flushPromises()

    expect(toastsOnScreen()).toEqual([['Password reset email sent.', UiTone.Success]])
  })

  /**
   * Opens the change-password dialog of the user and submits a new password.
   *
   * @param wrapper - The mounted screen.
   * @returns Resolves when the request has settled.
   */
  const changePassword = async (wrapper: Awaited<ReturnType<typeof mountUsersAt>>['wrapper']): Promise<void> => {
    await wrapper.get('[data-testid="users-actions-u1"]').trigger('click')
    await wrapper.get('[data-testid="users-password-u1"]').trigger('click')
    await wrapper.get('[data-testid="clients-change-password-input"]').setValue('s3cret-Pass')
    await wrapper.get('[data-testid="clients-change-password-save"]').trigger('click')
    await flushPromises()
  }

  it('confirms a changed password with a toast and closes the dialog', async () => {
    stubRoutes({
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
      'POST /api/admin/users/u1/change-password': () => new Response(null, { status: 204 }),
    })
    const wrapper = await mountUsers()

    await changePassword(wrapper)

    expect(toastsOnScreen()).toEqual([['Password changed.', UiTone.Success]])
    expect(wrapper.find('[data-testid="clients-change-password-input"]').exists()).toBe(false)
  })

  it('toasts the API sentence of a refused password change and keeps the dialog open', async () => {
    stubRoutes({
      'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }),
      'POST /api/admin/users/u1/change-password': () => problem('Passwords are managed in the sign-on service.', 400),
    })
    const wrapper = await mountUsers()

    await changePassword(wrapper)

    expect(toastsOnScreen()).toEqual([['Passwords are managed in the sign-on service.', UiTone.Danger]])
    expect(wrapper.find('[data-testid="clients-change-password-input"]').exists()).toBe(true)
  })

  it('offers no password action and names where passwords are managed when the sign-on service owns the accounts', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    useAuthStore().mode = AuthMode.Sso
    await flushPromises()

    // One action left, so the row draws it as a button instead of a menu.
    expect(wrapper.find('[data-testid="users-manage-u1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="users-reset-u1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="users-password-u1"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="users-passwords-managed-by-sso"]').text()).toContain('managed in the sign-on service')
  })

  it('shows no sign-on note where the deployment owns its accounts', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [user], totalCount: 1 }) })
    const wrapper = await mountUsers()

    useAuthStore().mode = AuthMode.Local
    await flushPromises()

    expect(wrapper.find('[data-testid="users-passwords-managed-by-sso"]').exists()).toBe(false)
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

  it('names the search in the empty state of a searched list', async () => {
    stubRoutes({ 'GET /api/admin/users': () => json({ items: [], totalCount: 0 }) })
    const { wrapper } = await mountUsersAt('/clients/users?search=nobody')

    expect(wrapper.text()).toContain('No users match your search')
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
  const askDelete = async (wrapper: Awaited<ReturnType<typeof mountUsersAt>>['wrapper']): Promise<void> => {
    await wrapper.get('[data-testid="users-actions-u1"]').trigger('click')
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
    expect(toastsOnScreen()).toEqual([['User deleted.', UiTone.Success]])
  })

  it('steps back a page when deleting the user empties the last one', async () => {
    let deleted = false
    const mock = stubRoutes({
      'GET /api/admin/users/u1': () => json({ ...user, accounts: [] }),
      'DELETE /api/admin/users/u1': () => {
        deleted = true
        return new Response(null, { status: 204 })
      },
      'GET /api/admin/users?': () => json({ items: deleted ? [] : [user], totalCount: deleted ? 20 : 21 }),
    })
    const { wrapper, router } = await mountUsersAt('/clients/users?page=2')
    await askDelete(wrapper)

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(router.currentRoute.value.query.page).toBeUndefined()
    })
    expect(urlsOf(mock).at(-1)).toBe('/api/admin/users?page=1&pageSize=20')
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
    expect(toastsOnScreen()).toEqual([])
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

    expect(toastsOnScreen()).toEqual([['The user owns an account.', UiTone.Danger]])
  })

  it('draws the table head from the schema columns while the first load is in flight', async () => {
    stubPendingFetch()
    const wrapper = await mountUsers()

    expect(renderedCaptions(wrapper)).toEqual(schemaCaptions(schema.table.columns))
  })
})
