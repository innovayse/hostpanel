/**
 * Plugin slot widget binding: the operator binds a widget of an installed plugin to products from
 * the Settings tab of the plugin's details page. The widget is listed with the slot it sits in, a pick adds a product chip,
 * Save sends the chosen ids and the reload shows what the server kept, a refused save shows the
 * API sentence on that widget, and a plugin with no widgets shows no section.
 */
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { HttpMethod } from '../src/types/http'
import type { PluginSlotWidgetBindingDto } from '../src/types/plugin'
import { bindableProducts, unboundWidget, widgetPlugin } from './fixtures/pluginSlots'
import { json, stubApi } from './support/stubApi'

/** Path of the widget list of the sample plugin. */
const LIST_PATH = `/api/admin/plugins/${widgetPlugin.id}/slot-widgets`

/** Path of the binding of the sample widget. */
const SAVE_PATH = `${LIST_PATH}/admin.service.panels/usage`

/** The routes every test shares: the registry knows nothing of the sample plugin (the page works without it) and no integration is listed. */
const baseRoutes = [
  json(HttpMethod.Get, '/api/admin/plugins', [widgetPlugin]),
  json(HttpMethod.Get, '/api/admin/integrations', []),
  json(HttpMethod.Get, `/api/admin/plugins/registry/${widgetPlugin.id}`, { type: 'PLUGIN_REGISTRY_PLUGIN_NOT_FOUND', title: 'Not found', status: 404, detail: 'No such plugin.' }, 404),
  json(HttpMethod.Get, '/api/products', bindableProducts),
]

/**
 * Opens the Settings tab of the sample plugin's details page.
 *
 * @param page - The Playwright page.
 */
const openSettings = async (page: Page): Promise<void> => {
  await page.goto(`/plugins/${widgetPlugin.id}`)
  await page.getByTestId('plugin-details-tab-settings').click()
}

test('the operator binds a widget to a product and the reload shows what was kept', async ({ page }) => {
  // The widget as the server holds it; the save binds it.
  const stored: PluginSlotWidgetBindingDto = unboundWidget()

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      ...baseRoutes,
      { method: HttpMethod.Get, path: LIST_PATH, respond: () => ({ body: [stored] }) },
      {
        method: HttpMethod.Put,
        path: SAVE_PATH,
        respond: (call) => {
          const { productIds } = call.body as { productIds: number[] }
          stored.productIds = productIds
          stored.isBound = productIds.length > 0
          return { body: stored }
        },
      },
    ],
  })

  await openSettings(page)
  const widget = page.getByTestId(`plugin-widget-${widgetPlugin.id}-usage`)
  await expect(widget).toContainText('Usage panel')
  await expect(widget).toContainText('admin.service.panels')
  await expect(widget.getByTestId('plugin-widget-save')).toBeDisabled()

  await widget.getByTestId('plugin-widget-add').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Business hosting' }).click()
  await expect(widget.getByTestId('plugin-widget-chosen')).toContainText('Business hosting')

  await widget.getByTestId('plugin-widget-save').click()

  await expect.poll(() => api.callsTo(HttpMethod.Put, SAVE_PATH).length).toBe(1)
  expect(api.callsTo(HttpMethod.Put, SAVE_PATH)[0]?.body).toEqual({ productIds: [4] })
  // The list is read again after the save, and the chip comes from that reload.
  await expect.poll(() => api.callsTo(HttpMethod.Get, LIST_PATH).length).toBe(2)
  await expect(widget.getByTestId('plugin-widget-chosen')).toContainText('Business hosting')
  await expect(widget.getByTestId('plugin-widget-save')).toBeDisabled()
})

test('a refused save shows the API sentence on the widget', async ({ page }) => {
  const sentence = 'The widget cannot be bound right now.'

  await stubApi(page, {
    signedIn: true,
    routes: [
      ...baseRoutes,
      json(HttpMethod.Get, LIST_PATH, [unboundWidget()]),
      json(HttpMethod.Put, SAVE_PATH, { type: 'PLUGIN_SLOT_WIDGET_NOT_FOUND', title: 'Refused', status: 404, detail: sentence }, 404),
    ],
  })

  await openSettings(page)
  const widget = page.getByTestId(`plugin-widget-${widgetPlugin.id}-usage`)
  await widget.getByTestId('plugin-widget-add').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Starter hosting' }).click()
  await widget.getByTestId('plugin-widget-save').click()

  await expect(widget.getByTestId('plugin-widget-error')).toContainText(sentence)
})

test('a plugin that declares no widget shows no binding section', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      ...baseRoutes,
      json(HttpMethod.Get, LIST_PATH, []),
    ],
  })

  await openSettings(page)
  await expect(page.getByTestId('plugin-details-settings')).toBeVisible()
  await expect(page.getByTestId(`plugin-slot-widgets-${widgetPlugin.id}`)).toHaveCount(0)
})
