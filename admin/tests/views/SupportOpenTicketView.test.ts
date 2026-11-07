/**
 * Smoke tests for SupportOpenTicketView: the form renders with placeholders, loads departments and
 * cannot be submitted without a client, and a refused create shows the API's sentence next to the
 * submit button, through the real stores and transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import SupportOpenTicketView from '@/views/SupportOpenTicketView.vue'
import { callOf, json, problem } from '../helpers/fetch'
import { mountView } from '../helpers/mount'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('SupportOpenTicketView', () => {
  it('loads the departments and renders the form with its placeholders', async () => {
    const mock = stubPathRoutes([{ path: '/tickets/departments', body: [{ id: 1, name: 'Support', email: 'support@example.test' }] }])

    const wrapper = await mountView(SupportOpenTicketView)

    expect(callOf(mock).url).toBe('/api/tickets/departments')
    expect(wrapper.get('[data-testid="support-open-ticket-subject"]').attributes('placeholder')).toBe('Ticket subject')
    expect(wrapper.get('[data-testid="support-open-ticket-message"]').attributes('placeholder')).toBeTruthy()
    expect(wrapper.get('[data-testid="support-open-ticket-client"]').attributes('placeholder')).toBeTruthy()
  })

  it('has no Send Email checkbox, because the department notification is not optional', async () => {
    stubPathRoutes([{ path: '/tickets/departments', body: [{ id: 1, name: 'Support', email: 'support@example.test' }] }])

    const wrapper = await mountView(SupportOpenTicketView)

    expect(wrapper.find('[data-testid="support-open-ticket-send-email"]').exists()).toBe(false)
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false)
  })

  it('shows the departments sentence with a Retry in place of the selector, then the selector again', async () => {
    let healthy = false
    stubRoutes({
      'GET /api/tickets/departments': () => (healthy ? json([{ id: 1, name: 'Support', email: 'support@example.test' }]) : problem('Departments unavailable.', 500)),
    })
    const wrapper = await mountView(SupportOpenTicketView)

    expect(wrapper.get('[data-testid="support-open-ticket-departments-error"]').text()).toBe('Departments unavailable.')
    expect(wrapper.find('[data-testid="support-open-ticket-department"]').exists()).toBe(false)

    healthy = true
    await wrapper.get('[data-testid="support-open-ticket-departments-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="support-open-ticket-departments-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="support-open-ticket-department"]').exists()).toBe(true)
  })

  it('cannot be submitted before a client is chosen, even with subject and message', async () => {
    stubPathRoutes([{ path: '/tickets/departments', body: [] }])
    const wrapper = await mountView(SupportOpenTicketView)

    await wrapper.get('[data-testid="support-open-ticket-subject"]').setValue('Help')
    await wrapper.get('[data-testid="support-open-ticket-message"]').setValue('Please help.')

    expect(wrapper.get('[data-testid="support-open-ticket-submit"]').attributes('disabled')).toBeDefined()
  })

  it('shows the API sentence next to the submit button when the create is refused', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    stubRoutes({
      'GET /api/tickets/departments': () => json([{ id: 1, name: 'Support', email: 'support@example.test' }]),
      'GET /api/clients': () => json({ items: [{ id: 5, firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' }], totalCount: 1 }),
      'POST /api/tickets': () => problem('The client is closed.', 409),
    })
    const wrapper = await mountView(SupportOpenTicketView)

    await wrapper.get('[data-testid="support-open-ticket-client"]').setValue('Ada')
    await vi.advanceTimersByTimeAsync(400)
    await flushPromises()
    await wrapper.get('[data-testid="support-open-ticket-client-5"]').trigger('click')
    await wrapper.get('[data-testid="support-open-ticket-subject"]').setValue('Help')
    await wrapper.get('[data-testid="support-open-ticket-message"]').setValue('Please help.')
    await wrapper.get('[data-testid="support-open-ticket-submit"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="support-open-ticket-error"]').text()).toBe('The client is closed.')
  })
})
