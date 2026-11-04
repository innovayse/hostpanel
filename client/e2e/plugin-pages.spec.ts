/**
 * Flow: a page a plugin adds to the client area (#645).
 *
 * A fake app plugin (`acme-sites`, served by the stub backend) adds a menu item and two pages. In each published
 * language and in both colour modes the reader opens the plugin from the sidebar, sees the list rendered from the
 * plugin's GET route with its markup shown as characters, opens a row, publishes the site (the stat re-reads),
 * sees only the link to the plugin's declared origin, and chats with the plugin over a streamed reply. Every label
 * comes from the API in the request's `X-Language`; the stub proves which language each call carried. The chat
 * reply is seen growing while the stub still holds the stream open, and a stop ends it with what had arrived.
 */
import type { Locator, Page } from '@playwright/test'
import { expect, test } from './support/test'
import { PLUGIN_CHAT_REPLY, PLUGIN_ID, PLUGIN_SITE, PLUGIN_TEXTS } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

/** Each published language with the path prefix that selects it (English has none). */
const LANGUAGES = [
  { language: 'en', prefix: '' },
  { language: 'ru', prefix: '/ru' },
  { language: 'hy', prefix: '/hy' },
] as const

/** Both colour modes, chosen through the visitor's own cookie as the colour-mode plugin reads it. */
const MODES = ['light', 'dark'] as const

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

for (const { language, prefix } of LANGUAGES) {
  for (const mode of MODES) {
    test(`the plugin pages work in ${language}, ${mode} mode`, async ({ page, context, request, baseURL }) => {
      await context.addCookies([{ name: 'color-mode', value: mode, url: baseURL ?? 'http://127.0.0.1:3100' }])
      const text = PLUGIN_TEXTS[language]

      await page.goto(`${prefix}/client`)
      await waitForHydration(page)
      if (mode === 'dark') {
        await expect(page.locator('html')).toHaveClass(/\bdark\b/)
      } else {
        await expect(page.locator('html')).not.toHaveClass(/\bdark\b/)
      }

      // The menu item the API offered, in the reader's language, opens the list page.
      const menuLink = page.getByTestId(`nav-client-apps-${PLUGIN_ID}-sites`)
      await expect(menuLink).toHaveText(text.menu)
      await menuLink.click()
      await expect(page.getByTestId('plugin-view')).toBeVisible()
      await expect(page.getByTestId('plugin-header')).toHaveText(text.sites)

      // The plugin's markup is text: the row shows the tags as characters and no <b> element exists.
      const row = page.getByTestId('plugin-table-row').first()
      await expect(row).toContainText(PLUGIN_SITE.name)
      await expect(page.getByTestId('plugin-table').locator('b')).toHaveCount(0)

      // A row opens the site page, its parameter filled from the row.
      await row.getByTestId('plugin-table-row-open').click()
      await expect(page).toHaveURL(new RegExp(`/client/apps/${PLUGIN_ID}/sites/${PLUGIN_SITE.id}$`))
      await expect(page.getByTestId('plugin-stat')).toContainText(PLUGIN_SITE.status)

      // Publishing calls the plugin and makes the stat read again.
      await page.getByTestId('plugin-action-button').click()
      await expect(page.getByTestId('plugin-stat')).toContainText('published')

      // Only the link to the declared origin is rendered; the other is left out entirely.
      await expect(page.getByTestId('plugin-link')).toHaveCount(1)
      await expect(page.getByTestId('plugin-link')).toHaveText(text.preview)

      // The chat reply streams in as the plugin writes it.
      await page.getByTestId('plugin-chat-input').fill('Hi')
      await page.getByTestId('plugin-chat-send').click()
      await expect(page.getByTestId('plugin-chat-assistant').last()).toHaveText(PLUGIN_CHAT_REPLY.join('').trim())

      // Every plugin call reached the backend in the reader's language.
      const pluginCalls = (await stubRequests(request)).filter(r => r.path.startsWith('/api/client/plugins/'))
      expect(pluginCalls.length).toBeGreaterThan(0)
      expect(new Set(pluginCalls.map(r => r.language))).toEqual(new Set([language]))
    })
  }
}

/**
 * Opens the fake plugin's site page and sends one chat message.
 *
 * @param page The page to drive.
 * @returns The locator of the assistant's reply.
 */
const sendChat = async (page: Page): Promise<Locator> => {
  await page.goto(`/client/apps/${PLUGIN_ID}/sites/${PLUGIN_SITE.id}`)
  await waitForHydration(page)
  await page.getByTestId('plugin-chat-input').fill('Is my site up?')
  await page.getByTestId('plugin-chat-send').click()
  return page.getByTestId('plugin-chat-assistant').last()
}

test('the chat reply grows while the plugin is still streaming, then the stream ends', async ({ page, request }) => {
  const reply = await sendChat(page)

  // The first piece shows while the stub still holds the stream open, so nothing on the way buffered it.
  await expect(reply).toHaveText((PLUGIN_CHAT_REPLY[0] ?? '').trim())
  await expect(page.getByTestId('plugin-chat-stop')).toBeVisible()
  await expect(reply).toHaveText(PLUGIN_CHAT_REPLY.join('').trim())
  await expect(page.getByTestId('plugin-chat-stop')).toBeHidden()

  const sent = (await stubRequests(request)).find(r => r.method === 'POST' && r.path === `/api/client/plugins/${PLUGIN_ID}/sites/${PLUGIN_SITE.id}/chat`)
  expect(sent?.body).toMatchObject({ message: 'Is my site up?' })
})

test('the chat stops when the reader presses stop, and keeps what had arrived', async ({ page }) => {
  const reply = await sendChat(page)
  await expect(reply).toHaveText((PLUGIN_CHAT_REPLY[0] ?? '').trim())

  await page.getByTestId('plugin-chat-stop').click()

  await expect(page.getByTestId('plugin-chat-stop')).toBeHidden()
  await expect(page.getByTestId('plugin-chat-error')).toHaveCount(0)
  // Wait past the stub's remaining pauses: nothing more may arrive after the stop.
  await page.waitForTimeout(PLUGIN_CHAT_REPLY.length * 200)
  await expect(reply).not.toHaveText(PLUGIN_CHAT_REPLY.join('').trim())
})

test('a plugin nobody installed answers the same not-found sentence', async ({ page }) => {
  await page.goto('/client/apps/nobody-installed/sites')
  await waitForHydration(page)

  await expect(page.getByTestId('plugin-page-not-found')).toContainText('This plugin page does not exist.')
})

test('an address no plugin page matches says so with the API\'s sentence', async ({ page }) => {
  await page.goto(`/client/apps/${PLUGIN_ID}/nowhere`)
  await waitForHydration(page)

  await expect(page.getByTestId('plugin-page-not-found')).toContainText('This plugin page does not exist.')
  await expect(page.getByTestId('plugin-view')).toHaveCount(0)
})
