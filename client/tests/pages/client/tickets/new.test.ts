/**
 * Tests for `pages/client/tickets/new.vue` against the real support store, API composable and
 * transport, with a stub BFF answering on a real socket (nothing of ours is mocked): the form, what
 * is sent and the confirmation.
 *
 * @module tests/pages/client/tickets/new.test
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mountPage, stubClientRuntime } from '~/tests/helpers/mountClient'
import NewTicketPage from '~/pages/client/tickets/new.vue'
import { bodyOf } from '~/tests/helpers/requestBody'
import { requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubServer } from '~/tests/helpers/stubServer'
import { startTransport } from '~/tests/helpers/stubTransport'
import { TicketPriority } from '~/types/ticketpriority'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The path that opens a ticket. */
const TICKETS_PATH = '/api/portal/client/tickets'

/** The path of the departments the form offers. */
const DEPARTMENTS_PATH = '/api/portal/public/departments'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

describe('new ticket page', () => {
  beforeEach(async () => {
    stubClientRuntime()
    table = { [`GET ${DEPARTMENTS_PATH}`]: { body: [{ id: 3, name: 'Billing' }] } }
    stub = await startTransport(routeTable(table))
  })

  afterEach(async () => {
    await stub.close()
  })

  it('shows the form with a placeholder on every field', async () => {
    const wrapper = await mountPage(NewTicketPage)

    expect(wrapper.find('[data-testid="ticket-subject"]').attributes('placeholder')).toBe('client.tickets.subjectPlaceholder')
    expect(wrapper.find('[data-testid="ticket-message"]').attributes('placeholder')).toBe('client.tickets.messagePlaceholder')
    expect(wrapper.find('[data-testid="ticket-success"]').exists()).toBe(false)
  })

  it('defaults the priority to medium', async () => {
    const wrapper = await mountPage(NewTicketPage)

    expect(wrapper.find('[data-testid="ticket-priority-Medium"]').classes()).toContain('bg-primary-700')
  })

  it('sends the typed ticket with the chosen priority and shows the confirmation', async () => {
    table[`POST ${TICKETS_PATH}`] = { body: {} }
    const wrapper = await mountPage(NewTicketPage)

    await wrapper.find('[data-testid="ticket-subject"]').setValue('Cannot log in')
    await wrapper.find('[data-testid="ticket-message"]').setValue('Please help')
    await wrapper.find('[data-testid="ticket-priority-High"]').trigger('click')
    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ticket-success"]').exists()).toBe(true))
    expect(bodyOf(requestsTo(stub.requests, TICKETS_PATH)[0])).toMatchObject({ subject: 'Cannot log in', message: 'Please help', priority: TicketPriority.High })
    expect(wrapper.find('[data-testid="ticket-view-all"]').attributes('href')).toBe('/client/tickets')
  })

  it('sends no department as null and shows the API field error under the department field', async () => {
    table[`POST ${TICKETS_PATH}`] = { status: 400, body: { errors: { departmentId: ['Select a department.'] } } }
    const wrapper = await mountPage(NewTicketPage)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ticket-department"]').attributes('aria-invalid')).toBe('true'))
    expect(bodyOf(requestsTo(stub.requests, TICKETS_PATH)[0])).toMatchObject({ departmentId: null })
    expect(wrapper.text()).toContain('Select a department.')
  })

  it('keeps the form and shows the refusal when the ticket is refused', async () => {
    table[`POST ${TICKETS_PATH}`] = { status: 400, body: { detail: 'Too many tickets.' } }
    const wrapper = await mountPage(NewTicketPage)

    await wrapper.find('form').trigger('submit')

    await waitUntil(() => expect(wrapper.find('[data-testid="ui-form-error"]').text()).toContain('Too many tickets.'))
    expect(wrapper.find('[data-testid="ticket-success"]').exists()).toBe(false)
  })
})
