/**
 * Storefront testimonials flow: the list shows the stored testimonial, creating one posts the
 * en / ru / hy quotes that were written and shows the new row, editing sends a PUT, and deleting
 * asks first and then removes the row.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { annTestimonial } from './fixtures/testimonials'
import type { Testimonial, TestimonialPayload } from '../src/types/testimonial'
import { fieldOf } from './support/locators'
import { collectionPath, stubCrud } from './support/stubCrud'

/** Path of the collection endpoint. */
const COLLECTION = collectionPath(ApiPath.AdminTestimonials)

/**
 * Stubs the testimonials endpoints over an in-memory list, so a write shows up in the next read.
 *
 * @param page - The page under test.
 * @param stored - The list the "database" holds; mutated by the writes.
 * @returns The recorder handle.
 */
const stubTestimonials = (page: Page, stored: Testimonial[]) => stubCrud(page, { root: ApiPath.AdminTestimonials, stored, newId: 40 })

test('the list shows the stored testimonial with its rating', async ({ page }) => {
  await stubTestimonials(page, [{ ...annTestimonial }])

  await page.goto('/storefront/testimonials')

  await expect(page.getByTestId(`testimonial-row-${annTestimonial.id}`)).toContainText('Ann Lee')
  await expect(page.getByTestId(`testimonial-row-${annTestimonial.id}`)).toContainText('Fast and calm hosting.')
  await expect(page.getByTestId(`testimonial-row-${annTestimonial.id}`)).toContainText('5 / 5')
})

test('creating a testimonial posts the written quotes and lists the new row', async ({ page }) => {
  const stored: Testimonial[] = [{ ...annTestimonial }]
  const api = await stubTestimonials(page, stored)

  await page.goto('/storefront/testimonials')
  await page.getByTestId('testimonials-add').click()
  await page.waitForURL('**/storefront/testimonials/create')

  await fieldOf(page, 'testimonial-form-name').fill('Bob Ray')
  await fieldOf(page, 'testimonial-form-company').fill('Globex')
  await fieldOf(page, 'testimonial-form-quote').fill('Great support.')
  await page.getByTestId('testimonial-form-tab-ru').click()
  await fieldOf(page, 'testimonial-form-quote').fill('Отличная поддержка.')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/testimonials')
  await expect(page.getByTestId('testimonial-row-40')).toContainText('Bob Ray')
  const body = api.callsTo(HttpMethod.Post, COLLECTION)[0]?.body as TestimonialPayload
  expect(body).toMatchObject({ name: 'Bob Ray', company: 'Globex', rating: 5, isActive: true })
  expect(body.translations).toEqual({ en: { role: '', quote: 'Great support.' }, ru: { role: '', quote: 'Отличная поддержка.' } })
})

test('editing a testimonial sends a PUT and shows the change', async ({ page }) => {
  const api = await stubTestimonials(page, [{ ...annTestimonial }])

  await page.goto('/storefront/testimonials')
  await page.getByTestId(`testimonial-actions-${annTestimonial.id}`).click()
  await page.getByTestId(`testimonial-edit-${annTestimonial.id}`).click()
  await page.waitForURL(`**/storefront/testimonials/${annTestimonial.id}/edit`)
  await expect(fieldOf(page, 'testimonial-form-name')).toHaveValue('Ann Lee')

  await fieldOf(page, 'testimonial-form-name').fill('Ann Smith')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/testimonials')
  await expect(page.getByTestId(`testimonial-row-${annTestimonial.id}`)).toContainText('Ann Smith')
  expect(api.callsTo(HttpMethod.Put, `${COLLECTION}/${annTestimonial.id}`)).toHaveLength(1)
})

test('deleting a testimonial asks first and then removes the row', async ({ page }) => {
  const api = await stubTestimonials(page, [{ ...annTestimonial }])

  await page.goto('/storefront/testimonials')
  await page.getByTestId(`testimonial-actions-${annTestimonial.id}`).click()
  await page.getByTestId(`testimonial-delete-${annTestimonial.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${annTestimonial.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`testimonial-row-${annTestimonial.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${annTestimonial.id}`)).toHaveLength(1)
})
