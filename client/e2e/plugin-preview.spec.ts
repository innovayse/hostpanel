/**
 * Flow: a workspace page's preview from a fresh sign-in, in each published language (#912).
 *
 * The reader opens the page signed out, signs in, and comes back to it without reloading by hand. A CSP cannot be
 * read from script, so the spec reads the header of the document on screen, and that the frame took the pane's height,
 * which an iframe without a definite box does not. The side menu stays hidden in every language.
 */
import { expect, test } from './support/test'
import { PLUGIN_ID, PLUGIN_FRAME_ORIGIN } from './support/fixtures.mjs'
import { resetStub, signInThroughForm, waitForHydration } from './support/helpers'

/** Each published language with the path prefix that selects it (English has none). */
const LANGUAGES = [
  { language: 'en', prefix: '' },
  { language: 'ru', prefix: '/ru' },
  { language: 'hy', prefix: '/hy' },
] as const

/** A frame this short was the height a browser gave it without a definite box. */
const SHORTEST_FRAME_PX = 400

for (const { language, prefix } of LANGUAGES) {
  test(`the preview opens after a sign-in and fills its pane in ${language}`, async ({ page, request }) => {
    await resetStub(request)
    const address = `${prefix}/client/apps/${PLUGIN_ID}/workspace`
    const documents: { url: string, policy: string | undefined }[] = []
    page.on('response', (response) => {
      if (response.request().isNavigationRequest() && response.frame() === page.mainFrame()) {
        documents.push({ url: response.url(), policy: response.headers()['content-security-policy'] })
      }
    })

    await page.goto(address)
    await waitForHydration(page)
    await signInThroughForm(page)

    const frame = page.getByTestId('plugin-preview-frame')
    await expect(frame).toBeVisible()
    await expect(frame).toHaveAttribute('src', `${PLUGIN_FRAME_ORIGIN}/__e2e/frame`)
    expect(documents.at(-1)?.url).toContain(address)
    expect(documents.at(-1)?.policy).toBe(`frame-src ${PLUGIN_FRAME_ORIGIN}`)
    expect((await frame.boundingBox())?.height ?? 0).toBeGreaterThan(SHORTEST_FRAME_PX)
    await expect(page.getByTestId('client-sidebar')).toBeHidden()
  })
}
