/**
 * Tests for ClientOpenTicketView: a failed departments load shows the error state with a Retry and
 * hides the form until it loads, a refused create toasts the API's sentence
 * and a created ticket opens its detail page, through the real stores and transport with
 * `fetch` stubbed.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import ClientOpenTicketView from '@/views/ClientOpenTicketView.vue'
import schema from '@/views/ClientOpenTicketView.schema'
import { ClientSection, clientItemPath } from '@/lib/routes'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { pickOption } from '../helpers/select'
import { stubRoutes } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'
import { fieldsOf } from '../helpers/schemaParity'

/** Route pattern the view is mounted under. */
const PATTERN = '/clients/:id/tickets/new'

/** Concrete URL the view is opened at. */
const URL = '/clients/5/tickets/new'

/** Department list the stubbed API answers with. */
const DEPARTMENTS = [{ id: 1, name: 'Support', email: 'support@example.test' }]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientOpenTicketView', () => {
  it('renders the form with its placeholders once the departments are loaded', async () => {
    stubRoutes({ 'GET /api/tickets/departments': () => json(DEPARTMENTS) })

    const { wrapper } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)

    expect(wrapper.find('[data-testid="client-open-ticket-departments-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="client-open-ticket-subject"]').attributes('placeholder')).toBe('Ticket subject')
    expect(wrapper.get('[data-testid="client-open-ticket-message"]').attributes('placeholder')).toBeTruthy()
    expect(wrapper.find('[data-testid="client-open-ticket-submit"]').exists()).toBe(true)
  })

  it('shows the error state with Retry and no form when the departments fail, then the form after a retry', async () => {
    let healthy = false
    const mock = stubRoutes({
      'GET /api/tickets/departments': () => (healthy ? json(DEPARTMENTS) : problem('Departments unavailable.', 500)),
    })

    const { wrapper } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)

    expect(wrapper.get('[data-testid="client-open-ticket-departments-error"]').text()).toBe('Departments unavailable.')
    expect(wrapper.find('[data-testid="client-open-ticket-submit"]').exists()).toBe(false)

    healthy = true
    await wrapper.get('[data-testid="client-open-ticket-departments-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-testid="client-open-ticket-departments-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-open-ticket-submit"]').exists()).toBe(true)
  })

  it('toasts the API sentence when the create is refused', async () => {
    stubRoutes({
      'GET /api/tickets/departments': () => json(DEPARTMENTS),
      'POST /api/tickets': () => problem('The client is closed.', 409),
    })
    const { wrapper, router } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)

    await wrapper.get('[data-testid="client-open-ticket-subject"]').setValue('Help')
    await pickOption(wrapper, 'client-open-ticket-department', 'Support')
    await wrapper.get('[data-testid="client-open-ticket-message"]').setValue('Please help.')
    await wrapper.get('[data-testid="client-open-ticket-submit"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toEqual(['The client is closed.'])
    expect(wrapper.find('[data-testid="client-open-ticket-departments-error"]').exists()).toBe(false)
    expect(router.currentRoute.value.path).toBe(URL)
  })

  it('opens the new ticket when the create succeeds', async () => {
    stubRoutes({
      'GET /api/tickets/departments': () => json(DEPARTMENTS),
      'POST /api/tickets': () => json(15),
    })
    const { wrapper, router } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)

    await wrapper.get('[data-testid="client-open-ticket-subject"]').setValue('Help')
    await pickOption(wrapper, 'client-open-ticket-department', 'Support')
    await wrapper.get('[data-testid="client-open-ticket-message"]').setValue('Please help.')
    await wrapper.get('[data-testid="client-open-ticket-submit"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.path).toBe(clientItemPath('5', ClientSection.Tickets, 15))
  })

  it('draws the trail of its client above the title: clients, the client, tickets, the page itself', async () => {
    stubRoutes({ 'GET /api/tickets/departments': () => json(DEPARTMENTS) })

    const { wrapper } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)
    const trail = wrapper.get('[data-testid="ui-breadcrumb"]')

    expect(trail.findAll('a').map((link) => link.attributes('href'))).toEqual(['/clients', '/clients/5', '/clients/5/tickets'])
    expect(trail.get('[data-testid="ui-page-header-crumb-current"]').text()).toBe(wrapper.get('h1').text())
    expect(wrapper.find('[data-testid="client-open-ticket-back"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-page-header-back"]').exists()).toBe(false)
    expect(trail.element.compareDocumentPosition(wrapper.get('h1').element) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('draws every section and field of its schema once the departments are loaded', async () => {
    stubRoutes({ 'GET /api/tickets/departments': () => json(DEPARTMENTS) })

    const { wrapper } = await mountScreen(ClientOpenTicketView, {}, URL, PATTERN)

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
    }
    for (const field of fieldsOf(schema)) {
      expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
    }
  })
})
