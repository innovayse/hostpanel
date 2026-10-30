/**
 * Servers flow: the list shows each server under its module, "test connection" reports the panel's
 * answer on the row, deleting asks first and removes the server, and an empty install offers the
 * first-server prompt.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { connectionOk, hostServer } from './fixtures/servers'
import type { ServerDto } from '../src/types/server'
import { json, stubApi } from './support/stubApi'

test('testing a connection shows the panel version on the server row', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Post, `/api/admin/servers/${hostServer.id}/test-connection`, connectionOk),
    ],
  })

  await page.goto('/servers')
  await expect(page.getByTestId(`server-row-${hostServer.id}`)).toContainText(hostServer.name)
  await expect(page.getByTestId(`server-state-${hostServer.id}`)).toBeVisible()

  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-test-${hostServer.id}`).click()

  await expect(page.getByTestId(`server-test-result-${hostServer.id}`)).toContainText(connectionOk.version ?? '')
  expect(api.callsTo(HttpMethod.Post, `/api/admin/servers/${hostServer.id}/test-connection`)).toHaveLength(1)
})

test('deleting a server asks first and then removes it', async ({ page }) => {
  const stored: ServerDto[] = [{ ...hostServer }]

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/servers', respond: () => ({ body: stored }) },
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      {
        method: HttpMethod.Delete,
        path: `/api/admin/servers/${hostServer.id}`,
        respond: () => {
          stored.splice(0, stored.length)
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-delete-${hostServer.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `/api/admin/servers/${hostServer.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId('servers-empty')).toBeVisible()
  expect(api.callsTo(HttpMethod.Delete, `/api/admin/servers/${hostServer.id}`)).toHaveLength(1)
})

test('a failed connection test shows the API sentence on the row', async ({ page }) => {
  const sentence = 'The control panel did not answer.'
  await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/servers', [hostServer]),
      json(HttpMethod.Get, '/api/admin/server-groups', []),
      json(HttpMethod.Post, `/api/admin/servers/${hostServer.id}/test-connection`, { type: 'UPSTREAM', title: 'Bad gateway', status: 502, detail: sentence }, 502),
    ],
  })

  await page.goto('/servers')
  await page.getByTestId(`server-row-${hostServer.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`server-test-${hostServer.id}`).click()

  await expect(page.getByTestId(`server-test-result-${hostServer.id}`)).toContainText(sentence)
})

test('an install without servers offers to add the first one', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/servers', []), json(HttpMethod.Get, '/api/admin/server-groups', [])],
  })

  await page.goto('/servers')

  await expect(page.getByTestId('servers-empty')).toBeVisible()
  await page.getByTestId('servers-add-first').click()
  await expect(page.getByTestId('server-form-name')).toBeVisible()
})
