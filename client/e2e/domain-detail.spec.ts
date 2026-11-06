/**
 * Flow: one domain's management page.
 *
 * The overview reads the domain; each panel changes one thing and the change is checked at the
 * backend as well as on screen: the auto-renew, registrar lock and ID protection switches (a switch
 * moves only when the backend accepts the change), the nameservers, the authorisation code and the
 * renewal order with the invoice it raises.
 */
import { expect, test } from './support/test'
import { DOMAIN, DOMAIN_NAMESERVERS, DOMAIN_ORDER, EPP_CODE, GATEWAY_METHODS, NEW_NAMESERVERS, REGISTRANT_CONTACT, WHOIS_RECORD } from './support/fixtures.mjs'
import { required, resetStub, scriptStub, signIn, stubRequests, waitForHydration } from './support/helpers'
import { HttpMethod } from '../types/httpmethod'

/**
 * The stub's path for one action on the domain under test.
 *
 * @param action Last path segment, naming the action.
 * @returns The backend path the stub records.
 */
const domainPath = (action: string): string => `/api/me/domains/${DOMAIN.id}/${action}`

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('the overview shows the domain and, while it is locked, no unlock warning', async ({ page }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)

  await expect(page.getByTestId('domain-header')).toContainText(DOMAIN.name)
  await expect(page.getByTestId('domain-overview')).toBeVisible()
  await expect(page.getByTestId('domain-quick-actions')).toBeVisible()
  await expect(page.getByTestId('domain-unlock-warning')).toHaveCount(0)
})

test('the list leads to the domain and the back link leads to the list', async ({ page }) => {
  await page.goto('/client/domains')
  await waitForHydration(page)

  await page.getByTestId(`domain-manage-${DOMAIN.id}`).click()
  await expect(page).toHaveURL(new RegExp(`/client/domains/${DOMAIN.id}$`))
  await expect(page.getByTestId('domain-header')).toBeVisible()

  await page.getByTestId('domain-back').click()
  await expect(page).toHaveURL(/\/client\/domains$/)
})

test('turning auto-renew off reaches the backend and moves the switch', async ({ page, request }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-autorenew').click()

  const toggle = page.getByTestId('domain-autorenew-row-toggle')
  await expect(toggle).toHaveAttribute('aria-checked', 'true')
  await toggle.click()
  await expect(toggle).toHaveAttribute('aria-checked', 'false')

  const sent = (await stubRequests(request)).find(r => r.method === 'PUT' && r.path === domainPath('auto-renew'))
  expect(sent?.body).toMatchObject({ value: false })
})

test('unlocking the domain raises the overview warning and ID protection can be turned on', async ({ page, request }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-lock').click()

  await page.getByTestId('domain-lock-row-toggle').click()
  await expect(page.getByTestId('domain-lock-row-toggle')).toHaveAttribute('aria-checked', 'false')

  await page.getByTestId('domain-idprotect-row-toggle').click()
  await expect(page.getByTestId('domain-idprotect-row-toggle')).toHaveAttribute('aria-checked', 'true')

  const requests = await stubRequests(request)
  expect(requests.find(r => r.method === 'PUT' && r.path === domainPath('lock'))?.body).toMatchObject({ value: false })
  expect(requests.find(r => r.method === 'PUT' && r.path === domainPath('whois-privacy'))?.body).toMatchObject({ value: true })

  // The state survives leaving the panel: the overview now warns about the unlocked domain.
  await page.getByTestId('domain-tab-overview').click()
  await expect(page.getByTestId('domain-unlock-warning')).toBeVisible()
})

test('a refused lock change leaves the switch where it was and says why', async ({ page, request }) => {
  await scriptStub(request, { method: HttpMethod.Put, path: domainPath('lock'), status: 400, code: 'LOCK_REFUSED', detail: 'E2E: the registrar refused the change.' })

  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-lock').click()
  await page.getByTestId('domain-lock-row-toggle').click()

  await expect(page.getByTestId('domain-toggle-error')).toContainText('E2E: the registrar refused the change.')
  await expect(page.getByTestId('domain-lock-row-toggle')).toHaveAttribute('aria-checked', 'true')
})

test('the authorisation code is requested and shown', async ({ page }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-lock').click()

  await page.getByTestId('domain-epp-request').click()

  await expect(page.getByTestId('domain-epp-message')).toContainText(EPP_CODE)
})

test('the nameservers are read and custom ones are saved', async ({ page, request }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-nameservers').click()

  await expect(page.getByTestId('domain-ns-1')).toHaveValue(required(DOMAIN_NAMESERVERS[0], 'first nameserver').host)
  await expect(page.getByTestId('domain-ns-2')).toHaveValue(required(DOMAIN_NAMESERVERS[1], 'second nameserver').host)

  await page.getByTestId('domain-ns-1').fill(required(NEW_NAMESERVERS[0], 'first new nameserver'))
  await page.getByTestId('domain-ns-2').fill(required(NEW_NAMESERVERS[1], 'second new nameserver'))
  await page.getByTestId('domain-ns-save').click()

  await expect(page.getByTestId('ui-form-success')).toBeVisible()
  const saved = (await stubRequests(request)).find(r => r.method === 'PUT' && r.path === domainPath('nameservers'))
  expect(saved?.body).toMatchObject({ nameservers: NEW_NAMESERVERS })
})

test('a renewal is ordered with the gateway and leads to its invoice', async ({ page, request }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-action-renew').click()

  await expect(page.getByTestId('domain-renew-summary')).toBeVisible()
  await expect(page.getByTestId('domain-renew-submit')).toBeEnabled()
  await page.getByTestId('domain-renew-submit').click()

  await expect(page.getByTestId('domain-renew-success')).toBeVisible()
  await expect(page.getByTestId('domain-renew-pay')).toHaveAttribute('href', `/client/invoices/${DOMAIN_ORDER.invoiceId}`)

  const order = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === domainPath('renew'))
  expect(order?.body).toMatchObject({ years: 1, paymentMethod: GATEWAY_METHODS[0]?.module })
})

test('the contact tab shows the registry record and saves the registrant contact wrapped in `contact`', async ({ page, request }) => {
  await page.goto(`/client/domains/${DOMAIN.id}`)
  await waitForHydration(page)
  await page.getByTestId('domain-tab-contact').click()

  await expect(page.getByTestId('domain-whois-record')).toContainText(WHOIS_RECORD.registrar)
  await expect(page.getByTestId('domain-whois-record')).toContainText(WHOIS_RECORD.registrant)

  // The form starts from the registrant the registrar holds, not from the profile.
  await expect(page.getByTestId('domain-contact-firstName')).toHaveValue(REGISTRANT_CONTACT.firstName)
  await expect(page.getByTestId('domain-contact-email')).toHaveValue(REGISTRANT_CONTACT.email)

  await page.getByTestId('domain-contact-custom').click()
  await page.getByTestId('domain-contact-firstName').fill('Rita')
  await page.getByTestId('domain-contact-save').click()
  await expect(page.getByTestId('ui-form-success')).toBeVisible()

  const saved = (await stubRequests(request)).find(r => r.method === 'PUT' && r.path === domainPath('whois'))
  expect(saved?.body).toMatchObject({ contact: { firstName: 'Rita' } })
})
