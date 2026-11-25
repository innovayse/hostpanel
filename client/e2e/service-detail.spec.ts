/**
 * Flow: one service's management page.
 *
 * Opens a set-up service from the list and reads its overview (details, server, related
 * invoices), changes the control-panel password (a too-short one is refused before anything is
 * sent) and requests a cancellation behind a confirmation. A service that still needs setup is
 * sent to the setup wizard instead.
 *
 * The service used has no domain: the client's server resolves the live DNS and certificate of a
 * service's domain, which the stub cannot answer, and for a service without one it skips the
 * lookup (see `SERVICE_MANAGED` in the fixtures).
 */
import { expect, test } from './support/test'
import { CANCEL_REASON, NEW_SERVICE_PASSWORD, PANEL_PLUGIN, PANEL_SSO_URL, SERVICE_INVOICE, SERVICE_MANAGED, SERVICE_PANEL, SERVICE_PENDING, SERVICE_SUSPENDED } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('the overview shows the service, its server and the invoices raised against it', async ({ page }) => {
  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('service-header')).toContainText(SERVICE_MANAGED.productName)
  await expect(page.getByTestId('service-details')).toBeVisible()
  await expect(page.getByTestId('service-server')).toContainText(SERVICE_MANAGED.username)
  await expect(page.getByTestId('service-setup-required')).toHaveCount(0)
  await expect(page.getByTestId('service-cancel-pending')).toHaveCount(0)
  await expect(page.getByTestId(`service-invoice-${SERVICE_INVOICE.id}`)).toBeVisible()
  await expect(page.getByTestId('service-usage').getByTestId('usage-bar')).toHaveCount(2)
  await expect(page.getByTestId('service-suspension-reason')).toHaveCount(0)
})

test('a suspended service says why it is suspended and shows no usage bar without reported figures', async ({ page }) => {
  await page.goto(`/client/services/${SERVICE_SUSPENDED.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('service-header')).toContainText(SERVICE_SUSPENDED.productName)
  await expect(page.getByTestId('service-suspension-reason')).toBeVisible()
  await expect(page.getByTestId('service-usage')).toHaveCount(0)
})

test('the list leads to the service and the back link leads to the list', async ({ page }) => {
  await page.goto('/client/services')
  await waitForHydration(page)

  await page.getByTestId(`service-card-${SERVICE_MANAGED.id}`).click()
  await expect(page).toHaveURL(new RegExp(`/client/services/${SERVICE_MANAGED.id}$`))
  await expect(page.getByTestId('service-header')).toBeVisible()

  await page.getByTestId('service-back').click()
  await expect(page).toHaveURL(/\/client\/services$/)
})

test('a too-short password is refused on the page and a good one reaches the backend', async ({ page, request }) => {
  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)
  await page.getByTestId('service-tab-password').click()

  await page.getByTestId('service-new-password').fill('short')
  await page.getByTestId('service-confirm-password').fill('short')
  await page.getByTestId('service-password-submit').click()
  await expect(page.getByTestId('ui-form-error')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.path.endsWith('/change-password'))).toBe(false)

  await page.getByTestId('service-new-password').fill(NEW_SERVICE_PASSWORD)
  await page.getByTestId('service-confirm-password').fill(NEW_SERVICE_PASSWORD)
  await page.getByTestId('service-password-submit').click()
  await expect(page.getByTestId('service-password-success')).toBeVisible()

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/me/services/${SERVICE_MANAGED.id}/change-password`)
  expect(sent?.body).toMatchObject({ newPassword: NEW_SERVICE_PASSWORD })
})

test('a cancellation is requested only after the confirmation', async ({ page, request }) => {
  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)
  await page.getByTestId('service-tab-cancel').click()

  await page.getByTestId('service-cancel-type-immediate').click()
  await page.getByTestId('service-cancel-reason').fill(CANCEL_REASON)
  await page.getByTestId('service-cancel-submit').click()

  // Nothing is sent until the dialog is confirmed.
  await expect(page.getByTestId('ui-confirm-dialog')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.path.endsWith('/cancel'))).toBe(false)

  await page.getByTestId('ui-confirm-confirm').click()
  await expect(page.getByTestId('service-cancel-success')).toBeVisible()

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/me/services/${SERVICE_MANAGED.id}/cancel`)
  expect(sent?.body).toMatchObject({ type: 'Immediate', reason: CANCEL_REASON })
})

test('a service without a control-panel account is sent to the setup wizard', async ({ page }) => {
  await page.goto(`/client/services/${SERVICE_PENDING.id}`)

  await expect(page).toHaveURL(new RegExp(`/client/services/${SERVICE_PENDING.id}/setup$`))
  await expect(page.getByTestId('setup-wizard')).toBeVisible()
})

test('the Open panel button shows the icon of the plugin that serves the server and opens the panel the API signs in to', async ({ page, context }) => {
  await page.goto(`/client/services/${SERVICE_PANEL.id}`)
  await waitForHydration(page)

  const icon = page.getByTestId('service-panel-login').getByTestId('service-panel-plugin-icon')
  await expect(icon).toHaveAttribute('alt', PANEL_PLUGIN.name)
  await expect(icon).toHaveAttribute('src', `/api/portal/client/plugins/${PANEL_PLUGIN.id}/icon`)
  await expect.poll(() => icon.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0)

  await context.route(PANEL_SSO_URL, (route) => route.fulfill({ body: '' }))
  const opened = context.waitForEvent('page')
  await page.getByTestId('service-panel-login').click()

  expect((await opened).url()).toBe(PANEL_SSO_URL)
})

test('the Open panel button keeps the generic icon on a server no plugin serves', async ({ page }) => {
  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('service-panel-plugin-icon')).toHaveCount(0)
})
