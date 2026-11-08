/**
 * Flow: a Publish button that sends the version it shows (#940), in a real browser.
 *
 * The fake app plugin's `stage` page reads its state once for both the button's label and the body of its call. After
 * the staged version moves on behind the open page (another tab, a finishing deployment), a click on the stale button is
 * refused by the plugin's route with its own sentence, nothing is published, and the button then shows the new version.
 * A click on the current button publishes exactly the version on its label.
 */
import type { APIRequestContext } from '@playwright/test'
import { expect, test } from './support/test'
import { PLUGIN_ID, STUB_URL } from './support/fixtures.mjs'
import { resetStub, signIn, stubRequests, waitForHydration } from './support/helpers'

/**
 * The version every publish request recorded by the stub asked for, oldest first.
 *
 * @param request - Playwright's API request context.
 * @returns The `expectedVersion` of each request to the stage publish route.
 */
const publishedVersions = async (request: APIRequestContext): Promise<number[]> => {
  const published = (await stubRequests(request)).filter(entry => entry.path.endsWith('/stage/publish'))
  return published.map(entry => (entry.body as { expectedVersion: number }).expectedVersion)
}

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
  // Nothing was published: the one publish the page sent carried the version it had shown (14), which the stub no longer
  // stages (15) and so refuses, and no publish for any other version was sent. The page now shows what it can publish.
  // (The "Version 13" text is not evidence of this: it is on the page before the click as well.)
  await expect(button).toHaveText('Publish version 15')
  expect(await publishedVersions(request)).toEqual([14])

  await button.click()
  await expect(page.getByTestId('plugin-stat')).toContainText('Version 15')

  expect(await publishedVersions(request)).toEqual([14, 15])
})
