/**
 * Storefront FAQ flow: the list shows the stored entry with its category, creating one posts the
 * written locale, and deleting asks first and then removes the row.
 */
import { HttpMethod } from '../src/types/http'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { sampleFaq } from './fixtures/storefront'
import type { Faq } from '../src/types/faq'
import { FaqCategory } from '../src/types/storefrontenums'
import { fieldOf } from './support/locators'
import { collectionPath, stubCrud } from './support/stubCrud'

/** Category labels from the English catalogue, so the spec follows the wording and names a category by its enum. */
const categoryLabels = (JSON.parse(readFileSync(join(process.cwd(), 'src/locales/en/storefront.json'), 'utf8')) as {
  faqs: { categories: Record<'general' | 'products', string> }
}).faqs.categories

/** Label the list shows for each category. */
const LABEL_OF: Record<FaqCategory, string> = {
  [FaqCategory.General]: categoryLabels.general,
  [FaqCategory.Products]: categoryLabels.products,
}

/** Path of the collection endpoint. */
const COLLECTION = collectionPath(ApiPath.AdminFaqs)

/**
 * Stubs the FAQ endpoints over an in-memory list, so a write shows up in the next read.
 *
 * @param page - The page under test.
 * @param stored - The list the "database" holds; mutated by the writes.
 * @returns The recorder handle.
 */
const stubFaqs = (page: Page, stored: Faq[]) => stubCrud(page, { root: ApiPath.AdminFaqs, stored, newId: 72 })

test('the list shows the stored question with its category', async ({ page }) => {
  await stubFaqs(page, [{ ...sampleFaq }])

  await page.goto('/storefront/faqs')

  await expect(page.getByTestId(`faq-row-${sampleFaq.id}`)).toContainText('How does it work?')
  await expect(page.getByTestId(`faq-row-${sampleFaq.id}`)).toContainText(LABEL_OF[sampleFaq.category])
})

test('creating an entry posts the written locale and lists the new row', async ({ page }) => {
  const api = await stubFaqs(page, [{ ...sampleFaq }])

  await page.goto('/storefront/faqs')
  await page.getByTestId('faqs-add').click()
  await page.waitForURL('**/storefront/faqs/create')
  await fieldOf(page, 'faq-form-question').fill('Can I cancel?')
  await fieldOf(page, 'faq-form-answer').fill('Yes, any time.')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/faqs')
  await expect(page.getByTestId('faq-row-72')).toContainText('Can I cancel?')
  expect(api.callsTo(HttpMethod.Post, COLLECTION)[0]?.body).toEqual({
    category: FaqCategory.General,
    sortOrder: 0,
    isActive: true,
    translations: { en: { question: 'Can I cancel?', answer: 'Yes, any time.' } },
  })
})

test('deleting an entry asks first and then removes the row', async ({ page }) => {
  const api = await stubFaqs(page, [{ ...sampleFaq }])

  await page.goto('/storefront/faqs')
  await page.getByTestId(`faq-row-${sampleFaq.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`faq-delete-${sampleFaq.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${sampleFaq.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`faq-row-${sampleFaq.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${sampleFaq.id}`)).toHaveLength(1)
})
