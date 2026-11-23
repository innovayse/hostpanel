/**
 * The servers a panel plugin serves, on the Settings tab of its details page: each server on a module the plugin declares is
 * listed with its status, the time of its last connection test (or that it was never tested) and a link to the Servers page; a
 * plugin whose module no server uses shows an empty note with a link; a plugin that serves no server module shows no section;
 * a refused read shows the API sentence and a retry; at phone width the rows stay inside the screen.
 */
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { HttpMethod } from '../src/types/http'
import { panelPlugin } from './fixtures/integrations'
import { notAPanel, panelServers, panelWithoutServers } from './fixtures/pluginServers'
import { json, stubApi } from './support/stubApi'
import type { StubApi, StubRoute } from './support/stubApi'

/** Path of the server list of the sample plugin. */
const SERVERS_PATH = `/api/admin/plugins/${panelPlugin.id}/servers`

/** The routes every test shares: the installed plugin, no integration, no slot widget and a registry that does not know the plugin. */
const baseRoutes: StubRoute[] = [
  json(HttpMethod.Get, '/api/admin/plugins', [panelPlugin]),
  json(HttpMethod.Get, '/api/admin/integrations', []),
  json(HttpMethod.Get, `/api/admin/plugins/registry/${panelPlugin.id}`, { type: 'PLUGIN_REGISTRY_PLUGIN_NOT_FOUND', title: 'Not found', status: 404, detail: 'No such plugin.' }, 404),
  json(HttpMethod.Get, `/api/admin/plugins/${panelPlugin.id}/slot-widgets`, []),
  json(HttpMethod.Get, '/api/products', []),
]

/**
 * Opens the Settings tab of the sample plugin details page.
 *
 * @param page - The Playwright page.
 */
const openSettings = async (page: Page): Promise<void> => {
  await page.goto(`/plugins/${panelPlugin.id}`)
  await page.getByTestId('plugin-details-tab-settings').click()
}

/**
 * Stubs the API with the shared routes and the answer for the servers.
 *
 * @param page - The Playwright page.
 * @param servers - The route that answers the servers of the sample plugin.
 * @returns The stub, to read the calls from.
 */
const stubWith = (page: Page, servers: StubRoute): Promise<StubApi> =>
  stubApi(page, { signedIn: true, routes: [...baseRoutes, servers] })

test('the Settings tab lists the servers of the plugin with their status, last test and a link to the server', async ({ page }) => {
  const api = await stubWith(page, json(HttpMethod.Get, SERVERS_PATH, panelServers))

  await openSettings(page)

  await expect(page.getByTestId('plugin-servers-card')).toBeVisible()
  await expect(page.getByTestId('plugin-servers-modules')).toContainText('example-panel')
  await expect(page.getByTestId('plugin-server-11')).toContainText('Frankfurt 1')
  await expect(page.getByTestId('plugin-server-state-11')).toHaveText('Online')
  await expect(page.getByTestId('plugin-server-tested-11')).toContainText('Last tested')
  await expect(page.getByTestId('plugin-server-link-11')).toHaveAttribute('href', '/servers?search=Frankfurt+1')
  await expect(page.getByTestId('plugin-server-state-12')).toHaveText('Not tested')
  await expect(page.getByTestId('plugin-server-tested-12')).toHaveText('Never tested')
  expect(api.callsTo(HttpMethod.Get, SERVERS_PATH)).toHaveLength(1)
})

test('a plugin whose module no server uses shows an empty note and a link to the Servers page', async ({ page }) => {
  await stubWith(page, json(HttpMethod.Get, SERVERS_PATH, panelWithoutServers))

  await openSettings(page)

  await expect(page.getByTestId('plugin-servers-empty')).toContainText('No server uses this plugin yet.')
  await expect(page.getByTestId('plugin-servers-open')).toHaveAttribute('href', '/servers')
  await expect(page.getByTestId('plugin-details-settings-empty')).toHaveCount(0)
})

test('a plugin that serves no server module shows no servers section', async ({ page }) => {
  await stubWith(page, json(HttpMethod.Get, SERVERS_PATH, notAPanel))

  await openSettings(page)

  await expect(page.getByTestId('plugin-details-settings-empty')).toBeVisible()
  await expect(page.getByTestId('plugin-servers-card')).toHaveCount(0)
})

test('a refused read shows the API sentence, and the retry reads the servers again', async ({ page }) => {
  let refuse = true
  const api = await stubWith(page, {
    method: HttpMethod.Get,
    path: SERVERS_PATH,
    respond: () => (refuse
      ? { status: 503, body: { type: 'SERVICE_UNAVAILABLE', title: 'Unavailable', status: 503, detail: 'The servers cannot be read right now.' } }
      : { body: panelServers }),
  })

  await openSettings(page)
  await expect(page.getByTestId('plugin-servers-error')).toContainText('The servers cannot be read right now.')

  refuse = false
  await page.getByTestId('plugin-servers-error-retry').click()

  await expect(page.getByTestId('plugin-server-11')).toBeVisible()
  expect(api.callsTo(HttpMethod.Get, SERVERS_PATH)).toHaveLength(2)
})

test.describe('at phone width', () => {
  test.use({ viewport: { width: 390, height: 800 } })

  test('the rows stay inside the screen', async ({ page }) => {
    await stubWith(page, json(HttpMethod.Get, SERVERS_PATH, panelServers))

    await openSettings(page)

    const card = page.getByTestId('plugin-servers-card')
    await expect(card).toBeVisible()
    const box = await card.boundingBox()
    expect(box).not.toBeNull()
    expect((box?.x ?? 0) + (box?.width ?? 0)).toBeLessThanOrEqual(390)
    await expect(page.getByTestId('plugin-server-link-11')).toBeVisible()
    await expect(page.getByTestId('plugin-server-state-12')).toBeVisible()
  })
})
