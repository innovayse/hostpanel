/**
 * UiRowActions keyboard behaviour: the menu opens with focus on the first item, arrow keys
 * cycle through items, and Escape closes the menu and returns focus to the trigger.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { hostServer } from './fixtures/servers'
import { json, stubApi } from './support/stubApi'

test('row actions menu: arrow keys navigate items and Escape returns focus to the trigger', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
    ],
  })

  await page.goto('/servers')

  const trigger = page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger')
  await trigger.click()

  // Menu is open and first item has focus.
  const menu = page.getByTestId('row-actions-menu')
  await expect(menu).toBeVisible()
  const firstItem = menu.getByTestId(`server-test-${hostServer.id}`)
  await expect(firstItem).toBeFocused()

  // ArrowDown moves focus to the next item.
  await page.keyboard.press('ArrowDown')
  const secondItem = menu.getByTestId(`server-ssh-${hostServer.id}`)
  await expect(secondItem).toBeFocused()

  // ArrowUp returns focus to the first item.
  await page.keyboard.press('ArrowUp')
  await expect(firstItem).toBeFocused()

  // Escape closes the menu and restores focus to the trigger.
  await page.keyboard.press('Escape')
  await expect(menu).toHaveCount(0)
  await expect(trigger).toBeFocused()
})
