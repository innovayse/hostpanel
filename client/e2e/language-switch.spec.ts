/**
 * Flow: the reader language reaches the backend (`X-Language` proof).
 *
 * The C# API answers every sentence in the language of the `X-Language` header, so a portal that
 * forgets to send it is answered in English whatever the reader picked, and nothing fails. The
 * contact form is the browser-initiated call that goes through every hop (page -> transport ->
 * BFF -> backend), so the header the stub logs is the proof for the whole chain.
 */
import { expect, test } from './support/test'
import { submitContactForm } from './support/forms'
import { resetStub, stubRequests, waitForHydration } from './support/helpers'

/** Each published language with the URL path that selects it (English has no prefix). */
const LANGUAGES = [
  { language: 'en', path: '/contact' },
  { language: 'ru', path: '/ru/contact' },
  { language: 'hy', path: '/hy/contact' },
  { language: 'es', path: '/es/contact' },
]

for (const { language, path } of LANGUAGES) {
  test(`the ${language} contact form reaches the backend with X-Language: ${language}`, async ({ page, request }) => {
    await resetStub(request)
    await page.goto(path)
    await waitForHydration(page)
    await expect(page.locator('html')).toHaveAttribute('lang', language)

    await submitContactForm(page)
    await expect(page.getByTestId('contact-success')).toBeVisible()

    await expect.poll(async () => {
      const requests = await stubRequests(request)
      return requests.find(r => r.method === 'POST' && r.path === '/api/contact')?.language
    }).toBe(language)
  })
}
