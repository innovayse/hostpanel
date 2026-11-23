/**
 * Hard load: opening a screen's address straight away draws the shell (sidebar and top bar) and the skeleton of the
 * screen at once, while the screen's own chunk is still downloading, and swaps the skeleton for the real screen when
 * the chunk arrives. The shell layout is part of the entry bundle, so only the screen below it waits.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { json, stubApi } from './support/stubApi'

test('a hard load of the add client form shows the sidebar, the top bar and the form skeleton until its chunk arrives', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/currencies', []),
      json(HttpMethod.Get, '/api/reference/countries', []),
    ],
  })
  // Hold the screen's chunk back until the spec lets it go; the shell and the skeleton schemas are in the entry bundle.
  // The chunk is `/assets/AddClientView-<hash>.js` in a build and `/src/views/AddClientView.vue` (with a query) on the dev server.
  let release: () => void = () => undefined
  const held = new Promise<void>((resolve) => {
    release = resolve
  })
  await page.route(/\/(assets\/AddClientView-[^/?]*\.js|src\/views\/AddClientView\.vue)(\?.*)?$/, async (route) => {
    await held
    await route.continue()
  })

  // `commit`: the load event waits for the held chunk's preload, which is exactly what this spec holds back.
  await page.goto('/clients/add', { waitUntil: 'commit' })

  await expect(page.getByTestId('sidebar-desktop')).toBeVisible()
  await expect(page.getByTestId('topbar')).toBeVisible()
  await expect(page.getByTestId('ui-skeleton-of')).toBeVisible()
  await expect(page.getByTestId('view-loading')).toHaveCount(0)
  await expect(page.getByTestId('add-client-submit')).toHaveCount(0)

  release()

  await expect(page.getByTestId('add-client-submit')).toBeVisible()
  await expect(page.getByTestId('ui-skeleton-of')).toHaveCount(0)
  await expect(page.getByTestId('topbar')).toBeVisible()
})
