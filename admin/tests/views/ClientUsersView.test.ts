/**
 * Mount tests for ClientUsersView: it renders against the real kit and settles on an API refusal
 * with a Retry, and removing a user is confirmed first and reports its outcome.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientUsersView from '@/views/ClientUsersView.vue'
import { mountClientView } from '../helpers/clientViews'
import { json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** A non-owner user of client 7. */
const member = {
  userId: 'u2',
  firstName: 'Grace',
  lastName: 'Hopper',
  email: 'grace@example.com',
  isOwner: false,
  permissions: 1,
  lastLoginAt: null,
  createdAt: '2026-01-01T00:00:00Z',
}

/**
 * Mounts the screen for client 7 and waits for its first load.
 *
 * @returns The wrapper.
 */
const mountUsers = async () => {
  const { wrapper } = await mountRouted(ClientUsersView, '/clients/:id/users', '/clients/7/users')
  await flushPromises()
  return wrapper
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientUsersView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientUsersView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientUsersView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('removes a user only after the confirmation and reports it', async () => {
    const mock = stubRoutes({
      'DELETE /api/clients/7/users/u2': () => new Response(null, { status: 204 }),
      'GET /api/clients/7/users': () => json([member]),
    })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="client-users-remove-u2"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-users-success"]').text()).toContain('removed')
  })

  it('shows the API sentence when the removal is refused and keeps the list', async () => {
    stubRoutes({
      'DELETE /api/clients/7/users/u2': () => problem('The user has open tickets.', 409),
      'GET /api/clients/7/users': () => json([member]),
    })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="client-users-remove-u2"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-users-error"]').text()).toBe('The user has open tickets.')
    expect(wrapper.find('[data-testid="client-users-load-error"]').exists()).toBe(false)
  })

  it('keeps the user when the confirmation is cancelled', async () => {
    const mock = stubRoutes({ 'GET /api/clients/7/users': () => json([member]) })
    const wrapper = await mountUsers()

    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="client-users-remove-u2"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)
  })

  it('reloads the list from the Retry of a refused load', async () => {
    let refused = true
    stubRoutes({ 'GET /api/clients/7/users': () => (refused ? problem('Users are unavailable.', 503) : json([member])) })
    const wrapper = await mountUsers()

    expect(wrapper.get('[data-testid="client-users-load-error"]').text()).toBe('Users are unavailable.')
    refused = false
    await wrapper.get('[data-testid="client-users-load-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-users-load-error"]').exists()).toBe(false)
    await wrapper.get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="client-users-remove-u2"]').exists()).toBe(true)
  })
})
