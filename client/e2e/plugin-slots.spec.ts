/**
 * Flow: plugin slots with a fake plugin. The dashboard shows the plugin's card and the service page
 * its tab; a widget that throws while it renders, and one whose view is slower than the widget's time
 * limit, each give way to a neutral note beside a working one, while the page stays usable; with no
 * plugin neither page shows any slot output. The slot reads carry the reader's language (en, ru, hy)
 * and the note is in that language; the cards render in dark mode too. The stub backend plays the
 * C# API and lists the fake widgets a spec scripts (`setPluginSlots`).
 */
import { expect, test } from './support/test'
import { SERVICE_MANAGED } from './support/fixtures.mjs'
import { resetStub, setPluginSlots, signIn, stubRequests, waitForHydration } from './support/helpers'

/** Each published language with its path prefix (English has none) and the widget note in it. */
const LANGUAGES = [
  { language: 'en', prefix: '', note: 'This part is not available right now.' },
  { language: 'ru', prefix: '/ru', note: 'Этот раздел сейчас недоступен.' },
  { language: 'hy', prefix: '/hy', note: 'Այս բաժինը հիմա հասանելի չէ։' },
] as const

/** The backend path of the dashboard slot's index. */
const DASHBOARD_SLOT_PATH = '/api/client/plugins/slots/client.dashboard.cards'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('with no plugin the dashboard and the service page show no slot output', async ({ page }) => {
  await page.goto('/client/dashboard')
  await waitForHydration(page)
  await expect(page.getByTestId('plugin-slot-client.dashboard.cards')).toHaveCount(0)

  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)
  await expect(page.getByTestId('service-header')).toBeVisible()
  await expect(page.locator('[data-testid^="service-tab-plugin."]')).toHaveCount(0)
})

test('a widget that throws gives way to a note and the working card beside it still renders', async ({ page, request }) => {
  await setPluginSlots(request, { 'client.dashboard.cards': ['ok', 'broken'] })

  await page.goto('/client/dashboard')
  await waitForHydration(page)

  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  await expect(page.getByTestId('plugin-slot-unavailable-acme-sites-broken')).toBeVisible()
  await expect(page.getByTestId('dashboard-title')).toBeVisible()
})

test('a widget slower than its time limit gives way to a note, the page and the other card untouched', async ({ page, request }) => {
  await setPluginSlots(request, { 'client.dashboard.cards': ['slow', 'ok'] })

  await page.goto('/client/dashboard')
  // The page never waits for a plugin: the header is there while the slow widget still loads.
  await expect(page.getByTestId('dashboard-title')).toBeVisible()
  await waitForHydration(page)

  await expect(page.getByTestId('plugin-slot-loading-acme-sites-slow')).toBeVisible()
  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  // The widget limit is 5 s and the stub answers the slow view after 8 s.
  await expect(page.getByTestId('plugin-slot-unavailable-acme-sites-slow')).toBeVisible({ timeout: 7000 })
  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  await expect(page.getByTestId('dashboard-title')).toBeVisible()
})

test('the service page lists the plugin tab, reads it for this service only, and opens its widget', async ({ page, request }) => {
  await setPluginSlots(request, { 'client.service.tabs': ['ok'] })

  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)
  await page.getByTestId('service-tab-plugin.acme-sites.ok').click()

  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  const slotReads = (await stubRequests(request)).filter(r => r.path.startsWith('/api/client/plugins/slots/client.service.tabs'))
  expect(slotReads.length).toBeGreaterThan(1)
  expect(slotReads.every(r => r.query === `?serviceId=${SERVICE_MANAGED.id}`)).toBe(true)
})

test('a throwing plugin tab shows a note and the rest of the service page keeps working', async ({ page, request }) => {
  await setPluginSlots(request, { 'client.service.tabs': ['broken', 'ok'] })

  await page.goto(`/client/services/${SERVICE_MANAGED.id}`)
  await waitForHydration(page)
  await page.getByTestId('service-tab-plugin.acme-sites.broken').click()
  await expect(page.getByTestId('plugin-slot-unavailable-acme-sites-broken')).toBeVisible()

  await page.getByTestId('service-tab-plugin.acme-sites.ok').click()
  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  await page.getByTestId('service-tab-overview').click()
  await expect(page.getByTestId('service-details')).toBeVisible()
})

for (const { language, prefix, note } of LANGUAGES) {
  test(`the ${language} dashboard reads its slot in ${language} and shows the widget note in ${language}`, async ({ page, request }) => {
    await setPluginSlots(request, { 'client.dashboard.cards': ['ok', 'broken'] })

    await page.goto(`${prefix}/client/dashboard`)
    await waitForHydration(page)
    await expect(page.locator('html')).toHaveAttribute('lang', language)

    await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
    await expect(page.getByTestId('plugin-slot-unavailable-acme-sites-broken')).toHaveText(note)
    const slotReads = (await stubRequests(request)).filter(r => r.path.startsWith(DASHBOARD_SLOT_PATH))
    expect(slotReads.length).toBeGreaterThan(0)
    expect(slotReads.every(r => r.language === language)).toBe(true)
  })
}

test('the plugin cards and the note render in dark mode', async ({ page, context, request, baseURL }) => {
  await setPluginSlots(request, { 'client.dashboard.cards': ['ok', 'broken'] })
  // The visitor's own choice, read by the server before the first paint (utils/colorMode.ts).
  await context.addCookies([{ name: 'color-mode', value: 'dark', url: baseURL ?? 'http://127.0.0.1:3100' }])

  await page.goto('/client/dashboard')
  await waitForHydration(page)

  await expect(page.locator('html')).toHaveClass(/\bdark\b/)
  await expect(page.getByTestId('plugin-slot-widget-acme-sites-ok')).toContainText('Site is live')
  await expect(page.getByTestId('plugin-slot-unavailable-acme-sites-broken')).toBeVisible()
})
