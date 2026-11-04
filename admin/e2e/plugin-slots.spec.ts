/**
 * Plugin slots flow: a fake plugin's widgets appear on the client summary page, the operator hides
 * one through the slot's setting and it leaves the page, and a page whose slot has no widgets shows
 * no slot at all.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { adaDetail } from './fixtures/clients'
import { emptyClientSummary } from './fixtures/clientSummary'
import type { PluginSlotWidgetDto } from '../src/types/pluginslot'
import { json, stubApi } from './support/stubApi'

/** An empty page of a paged list. */
const emptyPage = { items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

/** The endpoints the client summary page reads besides the slot. */
const summaryRoutes = [
  json(HttpMethod.Get, '/api/clients/7', adaDetail),
  json(HttpMethod.Get, '/api/clients/7/summary', emptyClientSummary),
  json(HttpMethod.Get, '/api/services', emptyPage),
  json(HttpMethod.Get, '/api/domains', emptyPage),
  json(HttpMethod.Get, '/api/billing/quotes/client/7', emptyPage),
]

/** The two widgets of the fake plugin. */
const widgets: PluginSlotWidgetDto[] = [
  { pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Site usage', order: 1, contextKeys: ['clientId'] },
  { pluginId: 'sites', id: 'builds', view: 'views/builds.json', title: 'Recent builds', order: 2, contextKeys: ['clientId'] },
]

test('hiding a widget adds its slot entry to the hide setting and removes it from the page', async ({ page }) => {
  let visible = [...widgets]
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      ...summaryRoutes,
      { method: HttpMethod.Get, path: '/api/admin/plugins/slots/admin.client.panels', respond: () => ({ body: { slotId: 'admin.client.panels', widgets: visible } }) },
      json(HttpMethod.Get, '/api/admin/settings', [
        { id: 5, key: 'plugins.hidden_slot_widgets', value: 'admin.service.panels:sites:usage', isSecret: false, hasValue: true },
      ]),
      {
        method: HttpMethod.Put,
        path: '/api/admin/settings/5',
        respond: () => {
          visible = visible.filter((widget) => widget.id !== 'usage')
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/clients/7/summary')

  await expect(page.getByTestId('plugin-widget-sites-usage')).toContainText('Site usage')
  await expect(page.getByTestId('plugin-widget-sites-builds')).toContainText('Recent builds')

  await page.getByTestId('plugin-widget-hide-sites-usage').click()

  await expect(page.getByTestId('plugin-widget-sites-usage')).toHaveCount(0)
  await expect(page.getByTestId('plugin-widget-sites-builds')).toBeVisible()
  // The service slot's entry stays; the new entry names the client slot.
  expect(api.callsTo(HttpMethod.Put, '/api/admin/settings/5')[0]?.body).toEqual({
    value: 'admin.service.panels:sites:usage,admin.client.panels:sites:usage',
  })
})

test('a slot nobody fills leaves the page as it was', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [...summaryRoutes, json(HttpMethod.Get, '/api/admin/plugins/slots/admin.client.panels', { slotId: 'admin.client.panels', widgets: [] })],
  })

  await page.goto('/clients/7/summary')

  await expect(page.getByTestId('client-summary-billing')).toBeVisible()
  await expect(page.locator('[data-testid^="plugin-slot-"]')).toHaveCount(0)
})
