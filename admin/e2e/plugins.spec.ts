/**
 * Plugin manager flow: installed plugins are listed, uploading an archive first shows what it asks for and its
 * changelog, then installs it as multipart data after the confirmation and shows the restart banner, restarting posts the command, and removing a
 * plugin asks first and then deletes it from the list, and the allow unsigned switch warns and saves its setting.
 */
import { HttpMethod } from '../src/types/http'
import { PluginSettingKey } from '../src/types/setting'
import { PluginHostAccess } from '../src/types/plugin'
import { expect, test } from '@playwright/test'
import { cwpPlugin } from './fixtures/integrations'
import { tinyPng } from './fixtures/settings'
import type { PluginListItemDto, PluginPackagePreviewDto } from '../src/types/plugin'
import type { Setting } from '../src/types/setting'
import { json, stubApi } from './support/stubApi'

/** What the server reads out of the uploaded archive: SSH access, a changelog and no version limit. */
const cwpPreview: PluginPackagePreviewDto = {
  id: cwpPlugin.id,
  name: cwpPlugin.name,
  version: cwpPlugin.version,
  author: cwpPlugin.author,
  description: cwpPlugin.description,
  hostAccess: [PluginHostAccess.Ssh],
  capabilities: [],
  changelog: 'First release.',
  minHostVersion: null,
  maxHostVersion: null,
  hostVersion: '2.0.0',
  compatible: true,
}

test('uploading an archive installs the plugin and offers the restart', async ({ page }) => {
  // The installed plugins as the server holds them; the install appends one.
  const stored: PluginListItemDto[] = []

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/plugins', respond: () => ({ body: stored }) },
      json(HttpMethod.Post, '/api/admin/plugins/inspect', cwpPreview),
      {
        method: HttpMethod.Post,
        path: '/api/admin/plugins/install',
        respond: () => {
          stored.push({ ...cwpPlugin, isLoaded: false })
          return { body: { requiresRestart: true } }
        },
      },
      { method: HttpMethod.Post, path: '/api/admin/plugins/restart', respond: () => ({ status: 204 }) },
    ],
  })

  await page.goto('/plugins')
  await expect(page.getByTestId('plugins-empty')).toBeVisible()

  await page.getByTestId('plugin-zip-input').setInputFiles({ name: 'cwp.zip', mimeType: 'application/zip', buffer: tinyPng })
  await expect(page.getByTestId('plugin-selected-name')).toContainText('cwp.zip')
  await page.getByTestId('plugin-install').click()

  await expect(page.getByTestId('plugin-install-access-ssh')).toBeVisible()
  await expect(page.getByTestId('plugin-install-changelog')).toContainText('First release.')
  expect(api.callsTo(HttpMethod.Post, '/api/admin/plugins/install')).toHaveLength(0)
  await page.getByTestId('plugin-install-confirm').click()

  await expect(page.getByTestId(`plugin-card-${cwpPlugin.id}`)).toContainText(cwpPlugin.name)
  await expect(page.getByTestId('plugins-restart-banner')).toBeVisible()
  const [install] = api.callsTo(HttpMethod.Post, '/api/admin/plugins/install')
  expect(install?.contentType).toContain('multipart/form-data')
  expect(install?.postData).toContain('filename="cwp.zip"')

  await page.getByTestId('plugins-restart').click()
  await expect.poll(() => api.callsTo(HttpMethod.Post, '/api/admin/plugins/restart').length).toBe(1)
})

test('removing a plugin deletes it from the list', async ({ page }) => {
  const stored: PluginListItemDto[] = [{ ...cwpPlugin }]

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/plugins', respond: () => ({ body: stored }) },
      {
        method: HttpMethod.Delete,
        path: `/api/admin/plugins/${cwpPlugin.id}`,
        respond: () => {
          stored.splice(0, stored.length)
          return { body: { requiresRestart: false } }
        },
      },
    ],
  })

  await page.goto('/plugins')
  await page.getByTestId(`plugin-remove-${cwpPlugin.id}`).click()
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId('plugins-empty')).toBeVisible()
  expect(api.callsTo(HttpMethod.Delete, `/api/admin/plugins/${cwpPlugin.id}`)).toHaveLength(1)
})

test('a refused install shows the API sentence', async ({ page }) => {
  const sentence = 'The archive does not contain a plugin manifest.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/plugins', []),
      json(HttpMethod.Post, '/api/admin/plugins/inspect', { type: 'PLUGIN_MANIFEST_INVALID', title: 'Invalid plugin', status: 400, detail: sentence }, 400),
    ],
  })

  await page.goto('/plugins')
  await page.getByTestId('plugin-zip-input').setInputFiles({ name: 'bad.zip', mimeType: 'application/zip', buffer: tinyPng })
  await page.getByTestId('plugin-install').click()

  await expect(page.getByTestId('plugins-feedback')).toContainText(sentence)
})

test('the allow unsigned switch warns, and saving it writes the setting', async ({ page }) => {
  // The one settings row the server holds; the save changes it.
  const row: Setting = { id: 9, key: PluginSettingKey.AllowUnsigned, value: 'false' }

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/plugins', []),
      { method: HttpMethod.Get, path: '/api/admin/settings', respond: () => ({ body: [row] }) },
      {
        method: HttpMethod.Put,
        path: '/api/admin/settings/9',
        respond: () => {
          row.value = 'true'
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/plugins')
  await expect(page.getByTestId('plugin-signing-warning')).toBeVisible()
  await expect(page.getByTestId('plugin-allow-unsigned')).toHaveAttribute('aria-checked', 'false')

  await page.getByTestId('plugin-allow-unsigned').click()
  await page.getByTestId('plugin-allow-unsigned-save').click()

  await expect(page.getByTestId('plugin-signing-active')).toBeVisible()
  const [put] = api.callsTo(HttpMethod.Put, '/api/admin/settings/9')
  expect(put?.postData).toContain('"value":"true"')
})
