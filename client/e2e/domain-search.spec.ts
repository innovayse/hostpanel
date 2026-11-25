/**
 * Flow: domain search.
 *
 * A visitor types a name; the page checks it against every offered extension and shows each as
 * available or taken. An available name can be added to the cart, a taken one cannot.
 */
import { expect, test } from './support/test'
import { FREE_TERM, TAKEN_DOMAIN } from './support/fixtures.mjs'
import { required, resetStub, stubRequests, waitForHydration } from './support/helpers'

test('an available name can be added to the cart', async ({ page }) => {
  await page.goto('/domains')
  await waitForHydration(page)

  await page.getByTestId('domain-search-input').fill(FREE_TERM)
  await page.getByTestId('domain-search-submit').click()

  const result = page.getByTestId(`domain-result-${FREE_TERM}.com`)
  await expect(result).toHaveAttribute('data-status', 'available')

  // Adding it disables the button, so the same name cannot be added twice.
  const add = page.getByTestId(`domain-add-${FREE_TERM}.com`)
  await add.click()
  await expect(add).toBeDisabled()
  await expect(page.getByTestId('nav-cart')).toBeVisible()
})

test('a registered name is reported as taken and offers no add button', async ({ page }) => {
  await page.goto('/domains')
  await waitForHydration(page)

  // The stub registers `taken-e2e.com` only; the same name under another extension is free.
  const name = required(TAKEN_DOMAIN.split('.')[0], 'name before the extension')
  await page.getByTestId('domain-search-input').fill(name)
  await page.getByTestId('domain-search-submit').click()

  await expect(page.getByTestId(`domain-result-${TAKEN_DOMAIN}`)).toHaveAttribute('data-status', 'taken')
  await expect(page.getByTestId(`domain-add-${TAKEN_DOMAIN}`)).toHaveCount(0)
  await expect(page.getByTestId(`domain-result-${name}.net`)).toHaveAttribute('data-status', 'available')
})

test('a malformed name is refused at once, before any request reaches the backend', async ({ page, request }) => {
  await resetStub(request)
  await page.goto('/domains')
  await waitForHydration(page)

  await page.getByTestId('domain-search-input').fill('bad name!')
  await page.getByTestId('domain-search-submit').click()

  await expect(page.getByTestId('domains-error')).toBeVisible()
  expect((await stubRequests(request)).some(r => r.path === '/api/domains/check')).toBe(false)
})

test('the search field and its button have the same height and sit on one line', async ({ page }) => {
  await page.goto('/domains')
  await waitForHydration(page)

  const input = await page.getByTestId('domain-search-input').boundingBox()
  const button = await page.getByTestId('domain-search-submit').boundingBox()
  if (!input || !button) {
    throw new Error('The search field and its button are not laid out.')
  }

  expect(Math.abs(button.height - input.height)).toBeLessThanOrEqual(1)
  expect(Math.abs(button.y - input.y)).toBeLessThanOrEqual(1)
})

test.describe('before the page is hydrated', () => {
  test.use({ javaScriptEnabled: false })

  test('Enter in the search field is a GET to the domains page that carries the typed name', async ({ page }) => {
    await page.goto('/domains')

    const input = page.getByTestId('domain-search-input')
    await input.fill(FREE_TERM)
    await input.press('Enter')

    await expect(page).toHaveURL(new RegExp(`/domains\\?domain=${FREE_TERM}$`))
  })
})

test('the page fits a phone screen without sideways scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/domains')
  await waitForHydration(page)

  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
  expect(overflow).toBeLessThanOrEqual(0)
})
