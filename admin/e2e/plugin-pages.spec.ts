/**
 * Plugin pages flow: a sample plugin declares an admin page and a menu item; the item shows in the
 * Add-ons section of the sidebar and opens the page, whose blocks read the plugin's admin routes, run its
 * action and form, and open another page of the plugin with a streamed chat. A panel without plugin menu
 * items shows no Add-ons section, and an address no plugin page answers shows the not-found state.
 */
import { expect, test } from '@playwright/test'
import { HttpMethod } from '../src/types/http'
import { dashboardStats } from './fixtures/dashboard'
import { initialSites, overviewPage, SAMPLE_PLUGIN, sampleIndex, sitePage } from './fixtures/pluginPages'
import { json, stubApi } from './support/stubApi'

/** The root of the sample plugin's admin routes. */
const EXT = `/api/admin/plugins/${SAMPLE_PLUGIN}/ext`

/** The page lookup of the sample plugin. */
const BY_PATH = `/api/admin/plugins/ui/${SAMPLE_PLUGIN}/pages/by-path`

test('the menu item opens the plugin page, whose blocks read, act and open another page with a chat', async ({ page }) => {
  const sites = initialSites()
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats),
      json(HttpMethod.Get, '/api/admin/plugins/ui', sampleIndex),
      {
        method: HttpMethod.Get,
        path: BY_PATH,
        respond: (call) => (new URLSearchParams(call.search).get('path') === '/sites/2' ? { body: sitePage } : { body: overviewPage }),
      },
      { method: HttpMethod.Get, path: `${EXT}/sites`, respond: () => ({ body: { total: sites.length, items: sites } }) },
      json(HttpMethod.Post, `${EXT}/sync`, { message: 'Synced.' }),
      {
        method: HttpMethod.Post,
        path: `${EXT}/sites`,
        respond: (call) => {
          const name = (call.body as { name?: string } | null)?.name ?? ''
          sites.push({ id: sites.length + 1, name, state: 'draft' })
          return { body: { message: `Created ${name}.` } }
        },
      },
      json(HttpMethod.Get, `${EXT}/sites/2`, { name: 'Blog' }),
    ],
  })
  // The chat answers a stream, which the JSON stub cannot; a route added later is matched first.
  await page.route(`**${EXT}/sites/2/chat`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      body: 'event: step\ndata: {"id":"a","label":"Reading the site","state":"done"}\n\nevent: delta\ndata: {"text":"The blog is a draft."}\n\nevent: done\ndata: {}\n\n',
    })
  })

  await page.goto('/dashboard')
  await expect(page.getByTestId('nav-section-addons-label')).toHaveText('Add-ons')
  await page.getByTestId(`nav-apps-${SAMPLE_PLUGIN}`).click()

  await expect(page).toHaveURL(`/apps/${SAMPLE_PLUGIN}`)
  await expect(page.getByTestId('plugin-header')).toContainText('Sample sites')
  await expect(page.getByTestId('plugin-stat')).toContainText('2')
  await expect(page.getByTestId('plugin-table-row')).toHaveCount(2)
  await expect(page.getByTestId('plugin-link')).toHaveAttribute('rel', 'noopener noreferrer')

  await page.getByTestId('plugin-action-button').click()
  await expect(page.getByTestId('plugin-header-message')).toHaveText('Synced.')

  await page.getByTestId('plugin-form-name').fill('Docs')
  await page.getByTestId('plugin-form-submit').click()
  await expect(page.getByTestId('plugin-form-message')).toHaveText('Created Docs.')
  await expect(page.getByTestId('plugin-table-row')).toHaveCount(3)
  await expect(page.getByTestId('plugin-stat')).toContainText('3')
  expect(api.callsTo(HttpMethod.Post, `${EXT}/sites`)[0]?.body).toEqual({ name: 'Docs' })

  await page.getByTestId('plugin-table-row-open').nth(1).click()
  await expect(page).toHaveURL(`/apps/${SAMPLE_PLUGIN}/sites/2`)
  await expect(page.getByTestId('plugin-header')).toContainText('Blog')

  await page.getByTestId('plugin-chat-input').fill('How is it?')
  await page.getByTestId('plugin-chat-send').click()
  await expect(page.getByTestId('plugin-chat-user')).toHaveText('How is it?')
  await expect(page.getByTestId('plugin-chat-assistant')).toContainText('The blog is a draft.')
  await expect(page.getByTestId('plugin-chat-step')).toHaveAttribute('data-state', 'done')
})

test('a panel whose plugins declare no menu item shows no Add-ons section', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/dashboard/stats', dashboardStats), json(HttpMethod.Get, '/api/admin/plugins/ui', { menus: [], pages: [] })],
  })

  await page.goto('/dashboard')

  await expect(page.getByTestId('nav-section-system')).toBeVisible()
  await expect(page.getByTestId('nav-section-addons')).toHaveCount(0)
})

test('an address no plugin page answers shows the not-found state with the API sentence', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/plugins/ui', { menus: [], pages: [] }),
      json(HttpMethod.Get, BY_PATH, { type: 'PLUGIN_PAGE_NOT_FOUND', title: 'Not Found', status: 404, detail: 'No such plugin page.' }, 404),
    ],
  })

  await page.goto(`/apps/${SAMPLE_PLUGIN}/missing`)

  await expect(page.getByTestId('plugin-page-not-found')).toContainText('No such plugin page.')
})
