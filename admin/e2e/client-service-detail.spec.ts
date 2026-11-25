/**
 * Client service detail flow: each section saves through its own endpoint with only its own
 * fields and confirms the save in a toast, and a refused termination date correction toasts the API's
 * localized sentence.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { activeServiceDetail, terminatedServiceDetail } from './fixtures/services'
import { PanelSyncField, PanelSyncOutcome, ServiceSection, ServiceStatus, type ServiceDetail } from '../src/types/service'
import { fieldOf, toastWith } from './support/locators'
import { json, stubApi, type RecordedCall, type StubApi, type StubRoute } from './support/stubApi'

/** Path of the service detail endpoint every section endpoint hangs off. */
const servicePath = `/api/services/${activeServiceDetail.id}`

/** Address of the service detail page in the admin. */
const pageUrl = `/clients/${activeServiceDetail.clientId}/services/${activeServiceDetail.id}`

/**
 * Path of one section's save endpoint.
 *
 * @param section - The section.
 * @returns The exact URL path the page PUTs to.
 */
const sectionPath = (section: ServiceSection): string => `${servicePath}/${section}`

/**
 * Stubs the backend the detail page reads, plus the given section endpoints.
 *
 * @param page - The page under test.
 * @param service - The service the detail endpoint answers with.
 * @param saves - The section endpoints this test lets the page call.
 * @returns The recorder handle.
 */
const stubServicePage = (page: Page, service: ServiceDetail, saves: StubRoute[]): Promise<StubApi> =>
  stubApi(page, {
    signedIn: true,
    routes: [
      ...saves,
      json(HttpMethod.Get, servicePath, service),
      json(HttpMethod.Get, `/api/clients/${adaDetail.id}`, adaDetail),
      json(HttpMethod.Get, '/api/services', { items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 }),
      json(HttpMethod.Get, '/api/admin/servers/options', []),
      json(HttpMethod.Get, '/api/products', []),
    ],
  })

/**
 * Every PUT the page sent to one of the service's section endpoints.
 *
 * @param api - The recorder handle.
 * @returns The section saves, in order.
 */
const sectionSaves = (api: StubApi): RecordedCall[] =>
  api.calls.filter((call) => call.method === HttpMethod.Put && call.path.startsWith(`${servicePath}/`))

/** One section save: how the test edits the section and what the endpoint must receive. */
interface SectionCase {
  /** The section under test. */
  section: ServiceSection
  /** The tab the section sits on. */
  tab: 'account' | 'billing' | 'notes'
  /** Changes the section's fields on the page. */
  edit: (page: Page) => Promise<void>
  /** Test id of the section's save button. */
  saveButton: string
  /** The exact body the section endpoint must receive. */
  body: Record<string, unknown>
}

/** The sections of a running service, each with its edit and expected body. */
const sectionCases: SectionCase[] = [
  {
    section: ServiceSection.HostingAccount,
    tab: 'account',
    edit: async (page) => {
      await fieldOf(page, 'client-service-domain').fill('shop.example.test')
    },
    saveButton: 'client-service-save-hosting',
    body: { serverId: null, domain: 'shop.example.test', provisioningRef: null, dedicatedIp: null, username: 'ada', password: null },
  },
  {
    section: ServiceSection.Billing,
    tab: 'billing',
    edit: async (page) => {
      await fieldOf(page, 'client-service-recurring').fill('14.5')
      await fieldOf(page, 'client-service-recurring').press('Tab')
    },
    saveButton: 'client-service-save-billing',
    body: {
      productId: activeServiceDetail.productId,
      quantity: 1,
      billingCycle: activeServiceDetail.billing.billingCycle,
      firstPaymentAmount: activeServiceDetail.billing.firstPaymentAmount,
      recurringAmount: 14.5,
      nextRenewalAt: '2026-12-01',
      paymentMethod: null,
      promotionCode: null,
      subscriptionId: null,
    },
  },
  {
    section: ServiceSection.Automation,
    tab: 'billing',
    edit: async (page) => {
      await page.getByTestId('client-service-auto-terminate').click()
      await fieldOf(page, 'client-service-terminate-reason').fill('The client is leaving.')
    },
    saveButton: 'client-service-save-automation',
    body: { autoRenew: true, overrideAutoSuspend: false, suspendUntil: null, autoTerminateEndOfCycle: true, autoTerminateReason: 'The client is leaving.' },
  },
  {
    section: ServiceSection.Notes,
    tab: 'notes',
    edit: async (page) => {
      await fieldOf(page, 'client-service-admin-notes').fill('Backups checked.')
    },
    saveButton: 'client-service-save-notes',
    body: { adminNotes: 'Backups checked.' },
  },
  {
    section: ServiceSection.Status,
    tab: 'account',
    edit: async (page) => {
      await page.getByTestId('client-service-status').getByTestId('ui-select-trigger').click()
      // The open list renders above the page (teleported to the body), so its options are not inside the field.
      await page.getByTestId('ui-select-panel').getByTestId('ui-select-option').nth(2).click()
    },
    saveButton: 'client-service-status-save',
    body: { status: ServiceStatus.Suspended },
  },
]

for (const sectionCase of sectionCases) {
  test(`saving the ${sectionCase.section} section sends only its fields to its own endpoint`, async ({ page }) => {
    const api = await stubServicePage(page, activeServiceDetail, [
      { method: HttpMethod.Put, path: sectionPath(sectionCase.section), respond: () => ({ status: 204 }) },
    ])

    await page.goto(pageUrl)
    await expect(fieldOf(page, 'client-service-domain')).toHaveValue(activeServiceDetail.domain ?? '')

    await page.getByTestId(`client-service-tab-${sectionCase.tab}`).click()
    await sectionCase.edit(page)
    await page.getByTestId(sectionCase.saveButton).click()

    await expect(toastWith(page, 'Changes saved successfully.')).toBeVisible()
    const saves = sectionSaves(api)
    expect(saves).toHaveLength(1)
    expect(saves[0]?.path).toBe(sectionPath(sectionCase.section))
    expect(saves[0]?.body).toEqual(sectionCase.body)
  })
}

test('correcting the termination date of a terminated service sends only the date', async ({ page }) => {
  const api = await stubServicePage(page, terminatedServiceDetail, [
    { method: HttpMethod.Put, path: sectionPath(ServiceSection.TerminationDate), respond: () => ({ status: 204 }) },
  ])

  await page.goto(pageUrl)
  await fieldOf(page, 'client-service-termination-date').fill('15/10/2026')
  await page.getByTestId('client-service-save-termination-date').click()

  await expect(toastWith(page, 'Changes saved successfully.')).toBeVisible()
  const saves = sectionSaves(api)
  expect(saves).toHaveLength(1)
  expect(saves[0]?.path).toBe(sectionPath(ServiceSection.TerminationDate))
  expect(saves[0]?.body).toEqual({ terminatedAt: '2026-10-15' })
})

test('a refused termination date correction toasts the API sentence and no success notice', async ({ page }) => {
  // The API answers in the language the page asked for; the page shows that sentence as is.
  const sentence = 'Only a terminated service has a termination date to correct.'
  const api = await stubServicePage(page, terminatedServiceDetail, [
    json(
      HttpMethod.Put,
      sectionPath(ServiceSection.TerminationDate),
      { type: 'SERVICE_NOT_TERMINATED', title: 'Conflict', status: 409, detail: sentence },
      409,
    ),
  ])

  await page.goto(pageUrl)
  await fieldOf(page, 'client-service-termination-date').fill('15/10/2026')
  await page.getByTestId('client-service-save-termination-date').click()

  await expect(toastWith(page, sentence)).toBeVisible()
  await expect(page.getByTestId('ui-toast').filter({ hasText: 'Changes saved successfully.' })).toHaveCount(0)
  expect(sectionSaves(api)).toHaveLength(1)
})

test('syncing a service with its panel asks first, sends the password choice and lists what was done', async ({ page }) => {
  const provisioned: ServiceDetail = { ...activeServiceDetail, account: { ...activeServiceDetail.account, provisioningRef: 'acct1' } }
  const syncPath = `/api/admin/services/${provisioned.id}/sync-panel`
  const api = await stubServicePage(page, provisioned, [
    json(HttpMethod.Post, syncPath, {
      fields: [
        { field: PanelSyncField.Status, outcome: PanelSyncOutcome.InSync, panelValue: 'Active' },
        { field: PanelSyncField.Package, outcome: PanelSyncOutcome.Applied, panelValue: 'legacy' },
        { field: PanelSyncField.IpAddress, outcome: PanelSyncOutcome.Applied, panelValue: '203.0.113.9' },
      ],
    }),
  ])

  await page.goto(pageUrl)
  await page.getByTestId('client-service-sync-panel').click()
  expect(api.calls.filter((call) => call.path === syncPath)).toHaveLength(0)

  await page.getByTestId('client-service-sync-push-password').click()
  await page.getByTestId('client-service-sync-confirm').click()

  await expect(page.getByTestId('client-service-sync-outcome-Package')).toHaveText('Applied')
  await expect(page.getByTestId('client-service-sync-outcome-Status')).toHaveText('In step')
  const syncs = api.calls.filter((call) => call.path === syncPath)
  expect(syncs).toHaveLength(1)
  expect(syncs[0]?.body).toEqual({ pushPassword: true })
})

test('the Open panel button shows the icon of the plugin that serves the server, and opens the panel through the generic sign-in route', async ({ page }) => {
  const provisioned: ServiceDetail = {
    ...activeServiceDetail,
    account: { ...activeServiceDetail.account, provisioningRef: 'acct1' },
    server: { name: 'srv', hostname: 'srv.example.test', ipAddress: null, panelPlugin: { id: 'acme-panel', name: 'Acme Panel', iconVersion: '1.2.0' } },
  }
  const ssoPath = `/api/provisioning/${provisioned.id}/panel-sso`
  const api = await stubServicePage(page, provisioned, [json(HttpMethod.Get, ssoPath, { url: 'https://panel.example.test/sso' })])
  await page.route('**/api/admin/plugins/acme-panel/icon', (route) =>
    route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8"><rect width="8" height="8"/></svg>' }))

  await page.goto(pageUrl)

  const icon = page.getByTestId('client-service-panel').getByTestId('plugin-avatar-image')
  await expect(icon).toHaveAttribute('alt', 'Acme Panel')
  await expect(icon).toHaveAttribute('src', '/api/admin/plugins/acme-panel/icon')
  await page.context().route('https://panel.example.test/**', (route) => route.fulfill({ body: '' }))
  await page.getByTestId('client-service-panel').click()
  await expect.poll(() => api.calls.filter((call) => call.path === ssoPath).length).toBe(1)
})

test('the Open panel button keeps the generic icon when no plugin serves the server', async ({ page }) => {
  const provisioned: ServiceDetail = { ...activeServiceDetail, account: { ...activeServiceDetail.account, provisioningRef: 'acct1' } }
  await stubServicePage(page, provisioned, [])

  await page.goto(pageUrl)

  await expect(page.getByTestId('client-service-panel')).toBeVisible()
  await expect(page.getByTestId('client-service-panel').getByTestId('plugin-avatar')).toHaveCount(0)
})

test('a panel that cannot sign anybody in shows how to sign in by hand, with no password', async ({ page }) => {
  const provisioned: ServiceDetail = { ...activeServiceDetail, account: { ...activeServiceDetail.account, provisioningRef: 'acct1' } }
  const notSupported = { type: 'PANEL_OPERATION_NOT_SUPPORTED', title: 'Not supported', status: 409, detail: 'The panel does not offer single sign on.' }
  await stubServicePage(page, provisioned, [
    json(HttpMethod.Get, `/api/provisioning/${provisioned.id}/panel-sso`, notSupported, 409),
    json(HttpMethod.Get, `/api/provisioning/${provisioned.id}/panel-login`, {
      username: 'ada',
      domain: 'shop.example.test',
      serverAddress: '203.0.113.9',
      loginUrl: 'https://panel.example.test:8083/login/',
    }),
  ])

  await page.goto(pageUrl)
  await page.getByTestId('client-service-panel').click()

  const dialog = page.getByTestId('client-service-panel-login-dialog')
  await expect(dialog.getByTestId('client-service-panel-login-link')).toHaveAttribute('href', 'https://panel.example.test:8083/login/')
  await expect(dialog.getByTestId('client-service-panel-login-link')).toHaveAttribute('target', '_blank')
  await expect(dialog.getByTestId('client-service-panel-login-username')).toHaveText('ada')
  await expect(dialog.getByTestId('client-service-panel-login-server')).toHaveText('203.0.113.9')
  await expect(dialog.locator('input')).toHaveCount(0)
})

test('a panel that offers neither a link nor login details toasts the API sentence', async ({ page }) => {
  const provisioned: ServiceDetail = { ...activeServiceDetail, account: { ...activeServiceDetail.account, provisioningRef: 'acct1' } }
  const sentence = 'The panel does not offer single sign on.'
  const notSupported = { type: 'PANEL_OPERATION_NOT_SUPPORTED', title: 'Not supported', status: 409, detail: sentence }
  await stubServicePage(page, provisioned, [
    json(HttpMethod.Get, `/api/provisioning/${provisioned.id}/panel-sso`, notSupported, 409),
    json(HttpMethod.Get, `/api/provisioning/${provisioned.id}/panel-login`, { ...notSupported, detail: 'No login details.' }, 409),
  ])

  await page.goto(pageUrl)
  await page.getByTestId('client-service-panel').click()

  await expect(toastWith(page, sentence)).toBeVisible()
  await expect(page.getByTestId('client-service-panel-login-dialog')).toHaveCount(0)
})
