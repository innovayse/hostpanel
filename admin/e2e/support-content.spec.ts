/**
 * Support content flows: publishing an announcement posts its title, body and flag and returns to
 * the list, adding a knowledgebase category posts it and shows it in the category grid, and a knowledgebase
 * article is written in several languages and edited later.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Announcement } from '../src/types/announcement'
import type { KbArticle, KbCategory } from '../src/types/kbarticle'
import { fieldOf } from './support/locators'
import { json, stubApi } from './support/stubApi'

test('publishing an announcement posts it and returns to the list', async ({ page }) => {
  // The announcements as the "database" holds them; the POST appends the new one.
  const stored: Announcement[] = []

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      {
        method: HttpMethod.Get,
        path: '/api/announcements',
        respond: () => ({ body: { items: stored, pageNumber: 1, pageSize: 20, totalCount: stored.length, totalPages: Math.ceil(stored.length / 20) } }),
      },
      {
        method: HttpMethod.Post,
        path: '/api/announcements',
        respond: (call) => {
          const sent = call.body as { title: string; content: string; isPublished: boolean }
          stored.push({ id: 1, createdAt: '2026-10-14T09:00:00Z', ...sent })
          return { body: 1 }
        },
      },
    ],
  })

  await page.goto('/support/announcements/new')
  await fieldOf(page, 'announcement-form-title').fill('Scheduled maintenance')
  await fieldOf(page, 'announcement-form-content').fill('The panel is read-only on Sunday night.')
  await page.getByTestId('announcement-form-published').check()
  await page.getByTestId('announcement-form-save').click()

  await page.waitForURL('**/support/announcements')
  await expect(page.getByTestId('announcements-row-1')).toContainText('Scheduled maintenance')
  expect(api.callsTo(HttpMethod.Post, '/api/announcements')[0]?.body).toEqual({
    title: 'Scheduled maintenance',
    content: 'The panel is read-only on Sunday night.',
    isPublished: true,
  })
})

test('adding a knowledgebase category posts it and shows it in the grid', async ({ page }) => {
  const stored: KbCategory[] = []

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/knowledgebase/categories', respond: () => ({ body: stored }) },
      json(HttpMethod.Get, '/api/admin/knowledgebase', []),
      {
        method: HttpMethod.Post,
        path: '/api/admin/knowledgebase/categories',
        respond: (call) => {
          const sent = call.body as { name: string; description: string; isHidden: boolean }
          stored.push({ id: 5, parentCategoryId: null, articleCount: 0, slug: 'getting-started', ...sent })
          return { body: 5 }
        },
      },
    ],
  })

  await page.goto('/support/knowledgebase')
  await expect(page.getByTestId('knowledgebase-category-add')).toBeDisabled()

  await fieldOf(page, 'knowledgebase-category-name').fill('Getting started')
  await fieldOf(page, 'knowledgebase-category-description').fill('First steps with the panel')
  await page.getByTestId('knowledgebase-category-add').click()

  await expect(page.getByTestId('knowledgebase-category-5')).toContainText('Getting started')
  expect(api.callsTo(HttpMethod.Post, '/api/admin/knowledgebase/categories')[0]?.body).toEqual({
    name: 'Getting started',
    description: 'First steps with the panel',
    isHidden: false,
  })
})

test('a refused announcement save shows the API sentence and stays on the form', async ({ page }) => {
  const sentence = 'An announcement with this title already exists.'
  await stubApi(page, {
    signedIn: true,
    routes: [json(HttpMethod.Post, '/api/announcements', { type: 'CONFLICT', title: 'Conflict', status: 409, detail: sentence }, 409)],
  })

  await page.goto('/support/announcements/new')
  await fieldOf(page, 'announcement-form-title').fill('Scheduled maintenance')
  await page.getByTestId('announcement-form-save').click()

  await expect(page.getByTestId('announcement-form-error')).toContainText(sentence)
  expect(new URL(page.url()).pathname).toBe('/support/announcements/new')
})

test('a knowledgebase article is written in two languages, listed, and edited to add a third', async ({ page }) => {
  const categories: KbCategory[] = [
    { id: 3, name: 'Billing', description: '', isHidden: false, parentCategoryId: null, articleCount: 0, slug: 'billing' },
  ]
  const articles: KbArticle[] = []

  const api = await stubApi(page, {
    signedIn: true,
    routes: [
      { method: HttpMethod.Get, path: '/api/admin/knowledgebase/categories', respond: () => ({ body: categories }) },
      { method: HttpMethod.Get, path: '/api/admin/knowledgebase', respond: () => ({ body: articles }) },
      {
        method: HttpMethod.Post,
        path: '/api/admin/knowledgebase',
        respond: (call) => {
          const sent = call.body as Pick<KbArticle, 'translations' | 'isPublished'> & { categoryId: number }
          articles.push({ id: 11, slug: 'how-to-pay', categoryName: 'Billing', ...sent })
          return { body: 11, status: 201 }
        },
      },
      {
        method: HttpMethod.Put,
        path: '/api/admin/knowledgebase/11',
        respond: (call) => {
          const sent = call.body as Pick<KbArticle, 'translations'> & { categoryId: number }
          articles[0] = { ...articles[0]!, ...sent }
          return { status: 204 }
        },
      },
    ],
  })

  await page.goto('/support/knowledgebase')
  await page.getByTestId('knowledgebase-tab-article').click()
  await page.getByTestId('knowledgebase-article-category').getByTestId('ui-select-trigger').click()
  await page.getByTestId('ui-select-option').filter({ hasText: 'Billing' }).click()
  await fieldOf(page, 'knowledgebase-article-title').fill('How to pay')
  await fieldOf(page, 'knowledgebase-article-content').fill('Use a card')
  await page.getByTestId('knowledgebase-article-locale-ru').click()
  await fieldOf(page, 'knowledgebase-article-title').fill('Как оплатить')
  await fieldOf(page, 'knowledgebase-article-content').fill('Картой')
  await page.getByTestId('knowledgebase-article-save').click()

  await expect(page.getByTestId('knowledgebase-article-11')).toContainText('How to pay')
  await expect(page.getByTestId('knowledgebase-article-11')).toContainText('how-to-pay')
  expect(api.callsTo(HttpMethod.Post, '/api/admin/knowledgebase')[0]?.body).toEqual({
    categoryId: 3,
    translations: { en: { title: 'How to pay', content: 'Use a card' }, ru: { title: 'Как оплатить', content: 'Картой' } },
    isPublished: false,
  })

  await page.getByTestId('knowledgebase-article-11-edit').click()
  await expect(fieldOf(page, 'knowledgebase-article-title')).toHaveValue('How to pay')
  await page.getByTestId('knowledgebase-article-locale-hy').click()
  await fieldOf(page, 'knowledgebase-article-title').fill('Ինչպես վճարել')
  await fieldOf(page, 'knowledgebase-article-content').fill('Քարտով')
  await page.getByTestId('knowledgebase-article-save').click()

  await expect(page.getByTestId('knowledgebase-feedback')).toContainText('Article saved.')
  expect(api.callsTo(HttpMethod.Put, '/api/admin/knowledgebase/11')[0]?.body).toMatchObject({
    categoryId: 3,
    translations: { hy: { title: 'Ինչպես վճարել', content: 'Քարտով' } },
  })
})
