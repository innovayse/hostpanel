/**
 * Plugin registry flow: with the registry switched on the plugin manager offers a Browse tab, the
 * tab lists what the registry offers, installing one posts the chosen version, shows the outcome and
 * the restart banner, and the listing then shows the plugin as installed.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { installedGateway, registryGateway, registryGatewayInstalled, registryTrustRow } from './fixtures/pluginRegistry'
import type { PluginListItemDto, PluginRegistryItemDto } from '../src/types/plugin'
import { json, stubApi } from './support/stubApi'

/** Listings per page the stubbed registry answers with. */
const pageSize = 20

test('browsing the registry and installing a plugin posts its version and shows it as installed', async ({ page }) => {
  // What the server holds: nothing installed, and one registry listing the install flips.
  const installed: PluginListItemDto[] = []
  let listing: PluginRegistryItemDto = registryGateway

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/plugins', respond: () => ({ body: installed }) },
      {
        method: HttpMethod.Get,
        path: '/api/admin/plugins/registry',
        respond: () => ({ body: { items: [listing], total: 1, page: 1, pageSize } }),
      },
      json(HttpMethod.Get, '/api/admin/plugins/registry/updates', []),
      json(HttpMethod.Get, '/api/admin/settings', [registryTrustRow]),
      {
        method: HttpMethod.Post,
        path: `/api/admin/plugins/registry/${registryGateway.id}/install`,
        respond: () => {
          installed.push(installedGateway)
          listing = registryGatewayInstalled
          return { body: { requiresRestart: true, stagedVersion: null } }
        },
      },
    ],
  })

  await page.goto('/plugins')
  await page.getByTestId('plugins-tab-browse').click()

  await expect(page.getByTestId(`plugin-card-${registryGateway.id}`)).toContainText(registryGateway.name)
  await expect(page.getByTestId(`plugin-card-verified-${registryGateway.id}`)).toBeVisible()
  await expect(page.getByTestId('plugin-registry-total')).toContainText('1')

  await page.getByTestId(`registry-install-${registryGateway.id}`).click()

  // The install runs behind the processing dialog, which reports the outcome and is closed by the operator.
  await expect(page.getByTestId('plugin-processing-outcome')).toBeVisible()
  await expect(page.getByTestId('plugin-processing-restart')).toBeVisible()
  await page.getByTestId('plugin-processing-close').click()
  await expect(page.getByTestId(`plugin-card-badge-active-${registryGateway.id}`)).toBeVisible()
  await expect(page.getByTestId(`plugin-card-version-${registryGateway.id}`)).toContainText(registryGateway.latestVersion)
  await expect(page.getByTestId(`registry-install-${registryGateway.id}`)).toHaveCount(0)
  await expect(page.getByTestId('plugins-restart-banner')).toBeVisible()

  const [install] = api.callsTo(HttpMethod.Post, `/api/admin/plugins/registry/${registryGateway.id}/install`)
  expect(install?.body).toEqual({ version: registryGateway.latestVersion, allowDowngrade: false })
})
