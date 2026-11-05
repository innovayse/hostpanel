/**
 * Integrations flow: a category page lists its integrations, a card opens the configuration form,
 * saving PUTs the enabled flag and the field values, and "test connection" posts the test.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import { bankTransfer, bankTransferDetail } from './fixtures/integrations'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('the payments page lists its integrations and a card opens the configuration', async ({ page }) => {
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Get, '/api/admin/integrations', [bankTransfer]), json(HttpMethod.Get, `/api/admin/integrations/${bankTransfer.slug}`, bankTransferDetail)],
  })

  await page.goto('/integrations/payments')
  await expect(page.getByTestId('integrations-grid')).toBeVisible()
  await expect(page.getByTestId(`integration-card-${bankTransfer.slug}`)).toContainText(bankTransfer.name)

  await page.getByTestId(`integration-card-${bankTransfer.slug}`).click()

  await page.waitForURL(`**/integrations/${bankTransfer.slug}/configure`)
  await expect(page.getByTestId('integration-config-form')).toBeVisible()
})

test('saving the configuration sends the enabled flag and the field values', async ({ page }) => {
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/integrations', [bankTransfer]),
      json(HttpMethod.Get, `/api/admin/integrations/${bankTransfer.slug}`, bankTransferDetail),
      { method: HttpMethod.Put, path: `/api/admin/integrations/${bankTransfer.slug}`, respond: () => ({ status: 204 }) },
    ],
  })

  await page.goto(`/integrations/${bankTransfer.slug}/configure`)
  await expect(page.getByTestId('integration-config-form')).toBeVisible()

  await page.getByTestId('integration-enabled').click()
  await fieldOf(page, 'integration-field-accountHolder').fill('Acme Ltd')
  await fieldOf(page, 'integration-field-iban').fill('GB82WEST12345698765432')
  await fieldOf(page, 'integration-field-webhookUrl').fill('https://hooks.example.com/bankTransfer')
  await page.getByTestId('integration-save').click()

  await expect.poll(() => api.callsTo(HttpMethod.Put, `/api/admin/integrations/${bankTransfer.slug}`).length).toBe(1)
  expect(api.callsTo(HttpMethod.Put, `/api/admin/integrations/${bankTransfer.slug}`)[0]?.body).toEqual({
    isEnabled: true,
    config: {
      accountHolder: 'Acme Ltd',
      iban: 'GB82WEST12345698765432',
      webhookUrl: 'https://hooks.example.com/bankTransfer',
      timeoutSeconds: '30',
    },
  })
})

test('testing the connection posts the test and a refusal shows the API sentence', async ({ page }) => {
  const sentence = 'The account details were rejected by the bank.'
  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      json(HttpMethod.Get, '/api/admin/integrations', [bankTransfer]),
      json(HttpMethod.Get, `/api/admin/integrations/${bankTransfer.slug}`, bankTransferDetail),
      json(
        HttpMethod.Post,
        `/api/admin/integrations/${bankTransfer.slug}/test`,
        { type: 'UPSTREAM', title: 'Rejected', status: 502, detail: sentence },
        502,
      ),
    ],
  })

  await page.goto(`/integrations/${bankTransfer.slug}/configure`)
  await page.getByTestId('integration-test').click()

  await expect.poll(() => api.callsTo(HttpMethod.Post, `/api/admin/integrations/${bankTransfer.slug}/test`).length).toBe(1)
  await expect(page.getByTestId('integration-status-result')).toContainText(sentence)
})
