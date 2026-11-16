/**
 * Ticket lists flow: on the staff ticket list and on the tickets tab of a client's profile, the search and the status,
 * priority and department filters of the filter dialog are sent to the API and kept in the address, each applied
 * filter is a chip that is removed from its own button, and a link with filters opens the filtered list.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { billingListed, openListed } from './fixtures/tickets'
import type { Department } from '../src/types/department'
import type { PaginatedList } from '../src/types/paginatedlist'
import { TicketPriority, TicketStatus } from '../src/types/ticket'
import type { TicketListItem } from '../src/types/ticket'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/** A closed, low priority ticket of the billing department: the one the filters must keep. */
const closedListed: TicketListItem = {
  ...billingListed,
  id: 44,
  subject: 'Old refund request',
  status: TicketStatus.Closed,
  priority: TicketPriority.Low,
}

/** The support departments the department filter offers; the technical one holds the open ticket, billing holds the rest. */
const departments: Department[] = [
  { id: 1, name: 'Technical', email: 'technical@example.test' },
  { id: 2, name: 'Billing', email: 'billing@example.test' },
]

/** Every ticket the stubbed lists hold. */
const allTickets: TicketListItem[] = [openListed, billingListed, closedListed]

/**
 * Stubs a ticket list endpoint so it narrows by the same query parameters the real API takes.
 *
 * @param path - The list's exact path.
 * @returns The stubbed route.
 */
const ticketList = (path: string): StubRoute => ({
  method: HttpMethod.Get,
  path,
  respond: (call) => {
    const query = new URLSearchParams(call.search)
    const term = query.get('search')?.toLowerCase() ?? ''
    const departmentName = departments.find((department) => String(department.id) === query.get('departmentId'))?.name
    const items = allTickets.filter(
      (ticket) =>
        ticket.subject.toLowerCase().includes(term)
        && (query.get('status') === null || ticket.status === query.get('status'))
        && (query.get('priority') === null || ticket.priority === query.get('priority'))
        && (departmentName === undefined || ticket.departmentName === departmentName),
    )
    const body: PaginatedList<TicketListItem> = { items, totalCount: items.length, pageNumber: 1, pageSize: 20, totalPages: 1 }
    return { body }
  },
})

/**
 * Stubs the staff ticket list and its departments.
 *
 * @param page - The page under test.
 * @returns The stubbed API, to read the requests it saw.
 */
const stubStaffList = (page: Page): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [ticketList('/api/tickets'), json(HttpMethod.Get, '/api/tickets/departments', departments)],
  })

/**
 * Chooses an option in one of the filter dialog's selects.
 *
 * @param page - The page under test.
 * @param testid - The `data-testid` of the select.
 * @param label - The exact label of the option to choose.
 * @returns Resolves once the option is clicked.
 */
const chooseOption = async (page: Page, testid: string, label: string): Promise<void> => {
  await page.getByTestId(testid).getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: new RegExp(`^${label}$`) }).click()
}

test('searching by subject sends the search and shows only the matching ticket', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/support/tickets')
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toBeVisible()
  await expect(page.getByTestId(`support-tickets-row-${closedListed.id}`)).toBeVisible()

  // The search box writes to the address after a pause and the list reloads from it.
  await fieldOf(page, 'support-tickets-search').fill('refund')
  await expect(page).toHaveURL(/search=refund/)

  await expect(page.getByTestId(`support-tickets-row-${closedListed.id}`)).toBeVisible()
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/tickets').at(-1)?.search).toContain('search=refund')
})

test('the status, priority and department filters are sent to the API, shown as chips and removed from their chips', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/support/tickets')
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toBeVisible()

  await page.getByTestId('support-tickets-filter').click()
  await chooseOption(page, 'support-tickets-filter-status', 'Closed')
  await chooseOption(page, 'support-tickets-filter-priority', 'Low')
  await chooseOption(page, 'support-tickets-filter-department', 'Billing')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Closed/)
  await expect(page).toHaveURL(/priority=Low/)
  await expect(page).toHaveURL(/departmentId=2/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Closed')
  await expect(page.getByTestId('filter-chip-priority')).toContainText('Low')
  await expect(page.getByTestId('filter-chip-departmentId')).toContainText('Billing')
  await expect(page.getByTestId(`support-tickets-row-${closedListed.id}`)).toBeVisible()
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toHaveCount(0)
  await expect(page.getByTestId(`support-tickets-row-${billingListed.id}`)).toHaveCount(0)
  const sent = api.callsTo(HttpMethod.Get, '/api/tickets').at(-1)?.search ?? ''
  expect(sent).toContain('status=Closed')
  expect(sent).toContain('priority=Low')
  expect(sent).toContain('departmentId=2')

  await page.getByTestId('filter-chip-remove-status').click()

  await expect(page.getByTestId('filter-chip-status')).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/tickets').at(-1)?.search).not.toContain('status=')
})

test('a list link with filters opens the filtered list', async ({ page }) => {
  const api = await stubStaffList(page)

  await page.goto('/support/tickets?priority=High&departmentId=1')

  await expect(page.getByTestId('filter-chip-priority')).toContainText('High')
  await expect(page.getByTestId('filter-chip-departmentId')).toContainText('Technical')
  await expect(page.getByTestId(`support-tickets-row-${openListed.id}`)).toBeVisible()
  await expect(page.getByTestId(`support-tickets-row-${closedListed.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/tickets')[0]?.search).toContain('priority=High')
})

test('the tickets tab of a client sends its filters to the API and shows them as chips', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/clients/7', adaDetail),
      ticketList('/api/tickets/client/7'),
      json(HttpMethod.Get, '/api/tickets/client/7/stats', { openedThisMonth: 1, openedLastMonth: 0, openedThisYear: 3, openedLastYear: 0 }),
      json(HttpMethod.Get, '/api/tickets/departments', departments),
    ],
  })

  await page.goto('/clients/7/tickets')
  await expect(page.getByTestId(`client-tickets-row-${openListed.id}`)).toBeVisible()

  await page.getByTestId('client-tickets-filter').click()
  await chooseOption(page, 'client-tickets-filter-status', 'Closed')
  await page.getByTestId('filter-apply').click()

  await expect(page).toHaveURL(/status=Closed/)
  await expect(page.getByTestId('filter-chip-status')).toContainText('Closed')
  await expect(page.getByTestId(`client-tickets-row-${closedListed.id}`)).toBeVisible()
  await expect(page.getByTestId(`client-tickets-row-${openListed.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Get, '/api/tickets/client/7').at(-1)?.search).toContain('status=Closed')
})
