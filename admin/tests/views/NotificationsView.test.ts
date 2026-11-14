/**
 * Mount tests of NotificationsView (the notification feed): it renders what the API sent, shows the empty state where it
 * has one, and shows the API's sentence when the load is refused.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import NotificationsView from '@/views/NotificationsView.vue'
import { mountRoutedSettled } from '../helpers/mount'
import { json, problem } from '../helpers/fetch'
import { stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('NotificationsView', () => {
  it('links each event to the record it is about', async () => {
    stubRoutes({
      'GET /api/admin/dashboard/activity': () => json([
        { type: 'ClientRegistered', message: 'Ada signed up', occurredAt: '2026-05-01T09:00:00Z', entityId: 7 },
      ]),
    })
    const wrapper = await mountRoutedSettled(NotificationsView, '/notifications')

    expect(wrapper.get('[data-testid="notification-0"]').attributes('href')).toBe('/clients/7')
    expect(wrapper.get('[data-testid="notification-0"]').text()).toContain('Ada signed up')
  })

  it('shows the empty state when the feed is empty', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => json([]) })
    const wrapper = await mountRoutedSettled(NotificationsView, '/notifications')

    expect(wrapper.get('[data-testid="notifications-empty"]').text()).toContain('No notifications')
  })

  it('shows the API sentence when the load is refused', async () => {
    stubRoutes({ 'GET /api/admin/dashboard/activity': () => problem('Not allowed.', 403) })
    const wrapper = await mountRoutedSettled(NotificationsView, '/notifications')

    expect(wrapper.get('[data-testid="notifications-error"]').text()).toContain('Not allowed.')
  })

  it('loads the feed again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/admin/dashboard/activity': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json([])
      },
    })
    const wrapper = await mountRoutedSettled(NotificationsView, '/notifications')

    await wrapper.get('[data-testid="notifications-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="notifications-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="notifications-empty"]').text()).toContain('No notifications')
  })

  it('draws the rows of the real list with bars where the events go while the feed loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const wrapper = await mountRoutedSettled(NotificationsView, '/notifications')

    const list = wrapper.get('[data-testid="notifications-list"]')
    expect(list.attributes('aria-busy')).toBe('true')
    expect(list.findAll('a')).toHaveLength(5)
    expect(list.find('a').attributes('href')).toBeUndefined()
    expect(wrapper.find('[data-testid="notifications-empty"]').exists()).toBe(false)
  })
})
