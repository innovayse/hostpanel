/**
 * Flow: selecting elements on a preview (#933), in a real browser with a real sandboxed frame, whose stub page plays the
 * site's picker. The frame's messages cross a real opaque-origin boundary, so `event.source` is what the browser says.
 */
import { expect, test } from './support/test'
import { PLUGIN_ID } from './support/fixtures.mjs'
import { resetStub, signInThroughForm, waitForHydration } from './support/helpers'

/** Each published language with its path prefix and the word on the select button. */
const LANGUAGES = [
  { language: 'en', prefix: '', select: 'Select' },
  { language: 'ru', prefix: '/ru', select: 'Выбрать' },
  { language: 'hy', prefix: '/hy', select: 'Ընտրել' },
] as const

for (const { language, prefix, select } of LANGUAGES) {
  test(`an element is selected in the frame, labelled by the server and sent in ${language}`, async ({ page, request }) => {
    await resetStub(request)
    await page.goto(`${prefix}/client/apps/${PLUGIN_ID}/workspace`)
    await waitForHydration(page)
    await signInThroughForm(page)

    const button = page.getByTestId('plugin-preview-select')
    await expect(button).toHaveText(select)
    await expect(page.getByTestId('plugin-preview-frame')).toHaveAttribute('sandbox', 'allow-scripts allow-forms')
    await button.click()
    await expect(page.getByTestId('plugin-preview-select-hint')).toBeVisible()

    const site = page.frameLocator('[data-testid="plugin-preview-frame"]')
    await site.getByTestId('site-title').click()
    await expect(page.getByTestId('plugin-chat-selection-chip')).toContainText('Server label')

    // A forged message from the panel's own window is not from the frame.
    await page.evaluate(() => {
      window.postMessage({ type: 'hostpanel.preview.selection', version: 1, release: 'r4', event: 'toggle', id: 'pages.0.blocks.0.props.heading' }, '*')
    })
    // The same from inside the frame, with a malformed id and with a wrong version, is refused by shape.
    await site.getByTestId('forge-bad-id').click()
    await site.getByTestId('forge-bad-version').click()
    await expect(page.getByTestId('plugin-chat-selection-chip')).toHaveCount(1)

    // Unknown to the server: the claim passes the shape and the server leaves it out.
    await site.getByTestId('forge-unknown').click()
    await expect(page.getByTestId('plugin-chat-selection-chip')).toHaveCount(1)

    await page.keyboard.press('Escape')
    await expect(page.getByTestId('plugin-preview-select-hint')).toBeHidden()
  })
}

test('@phone an element is selected by a tap with the panes stacked', async ({ page, request }) => {
  await resetStub(request)
  await page.goto(`/client/apps/${PLUGIN_ID}/workspace`)
  await waitForHydration(page)
  await signInThroughForm(page)

  // Stacked: the conversation's pane on top, the preview's below it, both as wide as the screen allows.
  const conversation = await page.getByTestId('plugin-split-pane-0').boundingBox()
  const preview = await page.getByTestId('plugin-split-pane-1').boundingBox()
  expect(conversation).not.toBeNull()
  expect(preview).not.toBeNull()
  expect(preview!.y).toBeGreaterThanOrEqual(conversation!.y + conversation!.height - 1)
  expect(Math.abs(preview!.x - conversation!.x)).toBeLessThan(2)

  await page.getByTestId('plugin-preview-select').tap()
  await expect(page.getByTestId('plugin-preview-select-hint')).toBeVisible()

  const site = page.frameLocator('[data-testid="plugin-preview-frame"]')
  await site.getByTestId('site-title').tap()
  await expect(page.getByTestId('plugin-chat-selection-chip')).toContainText('Server label')

  // A second tap on the same element takes it out again.
  await site.getByTestId('site-title').tap()
  await expect(page.getByTestId('plugin-chat-selection-chip')).toHaveCount(0)
})
