/**
 * Flow: a Publish button that sends the version it shows (#940), in a real browser.
 *
 * The fake app plugin's `stage` page reads its state once for both the button's label and the body of its call. After
 * the staged version moves on behind the open page (another tab, a finishing deployment), a click on the stale button is
 * refused by the plugin's route with its own sentence, nothing is published, and the button then shows the new version.
 * A click on the current button publishes exactly the version on its label.
 */
import { expect, test } from './support/test'
import { PLUGIN_ID, STUB_URL } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

test.beforeEach(async ({ context, request, baseURL }) => {
  await resetStub(request)
  await signIn(context, baseURL ?? 'http://127.0.0.1:3100')
})

test('a stale button is refused and the next one publishes the version it shows', async ({ page, request }) => {
  await page.goto(`/client/apps/${PLUGIN_ID}/stage`)
  await waitForHydration(page)

  const button = page.getByTestId('plugin-action-button')
  await expect(button).toHaveText('Publish version 14')
  await expect(page.getByTestId('plugin-stat')).toContainText('Version 13')

  // The staged version moves on behind the open page.
  await request.post(`${STUB_URL}/__e2e/plugin-stage`, { data: { version: 15 } })

  await button.click()
  await expect(page.getByTestId('plugin-action-message')).toContainText('The staged version changed')
  // Nothing was published, and the page now shows what it can publish.
  await expect(page.getByTestId('plugin-stat')).toContainText('Version 13')
  await expect(button).toHaveText('Publish version 15')

  await button.click()
  await expect(page.getByTestId('plugin-stat')).toContainText('Version 15')

  const published = (await stubRequests(request)).filter(entry => entry.path.endsWith('/stage/publish'))
  expect(published.map(entry => (entry.body as { expectedVersion: number }).expectedVersion)).toEqual([14, 15])
})
