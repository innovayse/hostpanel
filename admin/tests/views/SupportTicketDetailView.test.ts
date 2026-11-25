/**
 * Smoke tests for SupportTicketDetailView: the loaded ticket and its thread as bubbles, the side card
 * and its sheet on a phone, the reply composer and the close action, through the real store and
 * transport with `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import SupportTicketDetailView from '@/views/SupportTicketDetailView.vue'
import { CONVERSATION_CARD, DETAILS_CARD } from '@/views/SupportTicketDetailView.schema'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import { createTestI18n } from '../helpers/i18n'
import { mountView } from '../helpers/mount'
import { json, problem, stubFetch, callOf } from '../helpers/fetch'
import { stubPathRoutes, stubRoutes } from '../helpers/routes'
import { useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'

/** A ticket with one staff reply. */
const ticket = {
  id: 5,
  clientId: 3,
  subject: 'DNS is down',
  message: 'Nothing resolves.',
  status: TicketStatus.Open,
  priority: TicketPriority.Medium,
  department: { id: 1, name: 'Support' },
  assignee: { id: null, name: null },
  createdAt: '2026-10-01T00:00:00Z',
  replies: [{ id: 1, message: 'Looking into it.', authorName: 'Ann', isStaffReply: true, createdAt: '2026-10-01T01:00:00Z' }],
}

/** Where the view sits in the router. */
const location = { path: '/support/tickets/5', pattern: '/support/tickets/:id' }

/** The two endpoints the page loads on mount. */
const loadRoutes = [
  { path: '/tickets/5', body: ticket },
  { path: '/tickets/departments', body: [{ id: 1, name: 'Support', email: 'support@example.test' }] },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SupportTicketDetailView', () => {
  it('draws the real layout with chat bubble and field placeholders while the ticket loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))

    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.find('[data-testid="ui-page-header-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(createTestI18n().global.t(CONVERSATION_CARD.title))
    expect(wrapper.text()).toContain(createTestI18n().global.t(DETAILS_CARD.title))
    expect(wrapper.findAll('[data-testid="ticket-message-loading"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="ticket-composer"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
  })

  it('shows the ticket, its first message and the thread', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.get('[data-testid="ticket-title"]').text()).toContain('DNS is down')
    expect(wrapper.get('[data-testid="ticket-initial-message"]').text()).toContain('Nothing resolves.')
    expect(wrapper.findAll('[data-testid="ticket-reply"]')).toHaveLength(1)
    expect(wrapper.get('[data-testid="ticket-reply"]').text()).toContain('Looking into it.')
  })

  it('puts the client on the left and staff on the right of the thread', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.get('[data-testid="ticket-initial-message"][data-side="other"] [data-testid="ui-chat-bubble-body"]').text()).toBe('Nothing resolves.')
    expect(wrapper.get('[data-testid="ticket-reply"][data-side="own"] [data-testid="ui-chat-bubble-body"]').text()).toBe('Looking into it.')
    expect(wrapper.get('[data-testid="ticket-reply"]').text()).toContain('Ann')
  })

  it('shows the ticket details in a side card on a wide screen', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.get('[data-testid="ticket-details-open"]').classes()).toContain('lg:hidden')
    expect(wrapper.get('[data-testid="ticket-meta"]').text()).toContain('Unassigned')
    expect(wrapper.get('[data-testid="ticket-composer"] textarea').attributes('data-testid')).toBe('ticket-reply-message')
  })

  it('moves the ticket details into a sheet on a phone', async () => {
    vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(SupportTicketDetailView, location)
    expect(wrapper.find('[data-testid="ticket-meta"]').exists()).toBe(false)

    await wrapper.get('[data-testid="ticket-details-open"]').trigger('click')

    expect(wrapper.find('[data-testid="drawer-body"] [data-testid="ticket-meta"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ticket-department"]').exists()).toBe(true)
  })

  it('saves the department and priority chosen in the side card', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(SupportTicketDetailView, location)

    await wrapper.get('[data-testid="ticket-save"]').trigger('click')
    await flushPromises()

    const update = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'PUT')
    expect(update).toMatchObject({ url: '/api/tickets/5', body: { priority: 'Medium', departmentId: 1 } })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Changes saved successfully.'])
  })

  it('keeps the reply button disabled until the message is filled, then sends the reply without an author', async () => {
    const mock = stubPathRoutes([...loadRoutes, { path: '/tickets/5/reply', body: null }])
    const wrapper = await mountView(SupportTicketDetailView, location)
    const submit = wrapper.get('[data-testid="ticket-reply-submit"]')
    expect(submit.attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="ticket-reply-message"]').setValue('Fixed now.')
    expect(submit.attributes('disabled')).toBeUndefined()
    await submit.trigger('click')
    await flushPromises()

    const reply = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'POST')
    expect(reply).toMatchObject({ url: '/api/tickets/5/reply', body: { message: 'Fixed now.' } })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Reply sent.'])
  })

  it('toasts the API sentence when the reply is refused', async () => {
    stubRoutes({
      'POST /api/tickets/5/reply': () => problem('The ticket is closed.', 409),
      'GET /api/tickets/departments': () => json([]),
      'GET /api/tickets/5': () => json(ticket),
    })
    const wrapper = await mountView(SupportTicketDetailView, location)

    await wrapper.get('[data-testid="ticket-reply-message"]').setValue('Fixed now.')
    await wrapper.get('[data-testid="ticket-reply-submit"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => [toast.message, toast.tone])).toEqual([['The ticket is closed.', UiTone.Danger]])
  })

  it('deletes only after the danger confirmation and stays on a refusal', async () => {
    const mock = stubRoutes({
      'DELETE /api/tickets/5': () => problem('The ticket is locked.', 409),
      'GET /api/tickets/departments': () => json([]),
      'GET /api/tickets/5': () => json(ticket),
    })
    const wrapper = await mountView(SupportTicketDetailView, location)

    await wrapper.get('[data-testid="ticket-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(mock.mock.calls.some(([, init]) => (init as RequestInit | undefined)?.method === 'DELETE')).toBe(false)

    await wrapper.get('[data-testid="ticket-delete"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(useToastStore().toasts.map((toast) => [toast.message, toast.tone])).toEqual([['The ticket is locked.', UiTone.Danger]])
    expect(wrapper.get('[data-testid="ticket-title"]').text()).toContain('DNS is down')
  })

  it('closes the ticket with a status update', async () => {
    const mock = stubPathRoutes(loadRoutes)
    const wrapper = await mountView(SupportTicketDetailView, location)

    await wrapper.get('[data-testid="ticket-close"]').trigger('click')
    await flushPromises()

    const update = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.method === 'PUT')
    expect(update).toMatchObject({ url: '/api/tickets/5', body: { status: 'Closed' } })
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Ticket closed.'])
  })

  it('shows the API sentence when the ticket is refused', async () => {
    stubFetch(problem('Ticket not found.', 404))

    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.get('[data-testid="ticket-error"]').text()).toBe('Ticket not found.')
  })

  it('shows the departments sentence with a Retry in place of the selector, then the selector again', async () => {
    let healthy = false
    stubRoutes({
      'GET /api/tickets/departments': () => (healthy ? json([{ id: 1, name: 'Support', email: 'support@example.test' }]) : problem('Departments unavailable.', 500)),
      'GET /api/tickets/5': () => json(ticket),
    })
    const wrapper = await mountView(SupportTicketDetailView, location)

    expect(wrapper.get('[data-testid="ticket-departments-error"]').text()).toBe('Departments unavailable.')
    expect(wrapper.find('[data-testid="ticket-department"]').exists()).toBe(false)

    healthy = true
    await wrapper.get('[data-testid="ticket-departments-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="ticket-departments-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ticket-department"]').exists()).toBe(true)
  })

  it('loads the ticket again from the retry of a refused load', async () => {
    let calls = 0
    stubRoutes({
      'GET /api/tickets/departments': () => json([]),
      'GET /api/tickets/5': () => {
        calls += 1
        return calls === 1 ? problem('Ticket not found.', 404) : json(ticket)
      },
    })
    const wrapper = await mountView(SupportTicketDetailView, location)

    await wrapper.get('[data-testid="ticket-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="ticket-title"]').text()).toContain('DNS is down')
  })

  it('draws the trail above the title, the tickets list as a link and the ticket itself last', async () => {
    stubPathRoutes(loadRoutes)

    const wrapper = await mountView(SupportTicketDetailView, location)
    const trail = wrapper.get('[data-testid="ui-breadcrumb"]')

    expect(trail.findAll('a').map((link) => link.attributes('href'))).toEqual(['/support', '/support/tickets'])
    expect(trail.get('[data-testid="ui-page-header-crumb-current"]').text()).toBe(wrapper.get('h1').text())
    expect(wrapper.find('[data-testid="ticket-back"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    expect(trail.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
})
