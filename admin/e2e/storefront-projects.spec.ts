/**
 * Storefront projects flow: the list shows the stored project, creating one posts the written
 * locales and the gallery, and deleting asks first and then removes the row.
 */
import { HttpMethod } from '../src/types/http'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { ApiPath } from '../src/lib/apiPaths'
import { sampleProject } from './fixtures/storefront'
import type { Project, ProjectPayload } from '../src/types/project'
import { fieldOf } from './support/locators'
import { collectionPath, stubCrud } from './support/stubCrud'

/** Path of the collection endpoint. */
const COLLECTION = collectionPath(ApiPath.AdminProjects)

/**
 * Stubs the projects endpoints over an in-memory list, so a write shows up in the next read.
 *
 * @param page - The page under test.
 * @param stored - The list the "database" holds; mutated by the writes.
 * @returns The recorder handle.
 */
const stubProjects = (page: Page, stored: Project[]) => stubCrud(page, { root: ApiPath.AdminProjects, stored, newId: 62 })

test('the list shows the stored project with its slug', async ({ page }) => {
  await stubProjects(page, [{ ...sampleProject }])

  await page.goto('/storefront/projects')

  await expect(page.getByTestId(`project-row-${sampleProject.id}`)).toContainText('Sample Site')
  await expect(page.getByTestId(`project-row-${sampleProject.id}`)).toContainText('sample-site')
})

test('creating a project posts its slug, gallery and written locales', async ({ page }) => {
  const api = await stubProjects(page, [{ ...sampleProject }])

  await page.goto('/storefront/projects')
  await page.getByTestId('projects-add').click()
  await page.waitForURL('**/storefront/projects/create')
  await fieldOf(page, 'project-form-slug').fill('second-site')
  await fieldOf(page, 'project-form-title').fill('Second Site')
  await page.getByTestId('project-form-image-add').click()
  await fieldOf(page, 'project-form-image-0-url').fill('https://cdn.example.com/second.png')
  await page.getByTestId('settings-form-save').click()

  await page.waitForURL('**/storefront/projects')
  await expect(page.getByTestId('project-row-62')).toContainText('Second Site')
  const body = api.callsTo(HttpMethod.Post, COLLECTION)[0]?.body as ProjectPayload
  expect(body).toMatchObject({ slug: 'second-site', imageUrls: ['https://cdn.example.com/second.png'] })
  expect(Object.keys(body.translations)).toEqual(['en'])
})

test('deleting a project asks first and then removes the row', async ({ page }) => {
  const api = await stubProjects(page, [{ ...sampleProject }])

  await page.goto('/storefront/projects')
  await page.getByTestId(`project-row-${sampleProject.id}`).getByTestId('row-actions-trigger').click()
  await page.getByTestId(`project-delete-${sampleProject.id}`).click()
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${sampleProject.id}`)).toHaveLength(0)
  await page.getByTestId('dialog-confirm').click()

  await expect(page.getByTestId(`project-row-${sampleProject.id}`)).toHaveCount(0)
  expect(api.callsTo(HttpMethod.Delete, `${COLLECTION}/${sampleProject.id}`)).toHaveLength(1)
})
