/**
 * Smoke tests for ProjectFormView: create and edit modes, the locale tabs, the gallery, saving
 * through the store at network level, and the refusal and missing-entry messages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import ProjectFormView from '@/views/ProjectFormView.vue'
import schema from '@/views/ProjectFormView.schema'
import type { Project, ProjectPayload } from '@/types/project'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A stored project with two gallery images and English text. */
const stored: Project = {
  id: 3,
  slug: 'site',
  imageUrls: ['/img/a.png', '/img/b.png'],
  url: 'https://site.example.com',
  sortOrder: 2,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { title: 'Site', summary: 'A site', description: 'Long text' } },
}

/** Answers for what the form loads. */
const routes = { 'GET /admin/projects': () => json([stored]) }

/**
 * Reads the JSON body of the first request with a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The verb to look for.
 * @returns The parsed payload.
 */
const bodyOf = (mock: ReturnType<typeof vi.fn>, method: string): ProjectPayload => {
  const call = mock.mock.calls.find((candidate) => (candidate[1] as RequestInit | undefined)?.method === method)
  return JSON.parse((call?.[1] as RequestInit | undefined)?.body as string) as ProjectPayload
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProjectFormView', () => {
  it('opens an empty create form with the trail and the create button', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create project')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create project')
    expect(wrapper.find('[data-testid="project-form-image-0-url"]').exists()).toBe(false)
  })

  it('saves a new project with trimmed text and without empty gallery slots', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/projects': () => json(9) })
    const { wrapper, router } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    await wrapper.get('[data-testid="project-form-slug"]').setValue(' shop ')
    await wrapper.get('[data-testid="project-form-title"]').setValue(' Shop ')
    await wrapper.get('[data-testid="project-form-summary"]').setValue(' A shop ')
    await wrapper.get('[data-testid="project-form-image-add"]').trigger('click')
    await wrapper.get('[data-testid="project-form-image-add"]').trigger('click')
    await wrapper.get('[data-testid="project-form-image-0-url"]').setValue('/img/shop.png')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/projects')
    })

    expect(bodyOf(mock, 'POST')).toMatchObject({
      slug: 'shop',
      imageUrls: ['/img/shop.png'],
      translations: { en: { title: 'Shop', summary: 'A shop', description: '' } },
    })
  })

  it('keeps each locale tab separate', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    await wrapper.get('[data-testid="project-form-title"]').setValue('English title')
    await wrapper.get('[data-testid="project-form-tab-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="project-form-title"]').element as HTMLInputElement).value).toBe('')

    await wrapper.get('[data-testid="project-form-tab-en"]').trigger('click')
    expect((wrapper.get('[data-testid="project-form-title"]').element as HTMLInputElement).value).toBe('English title')
  })

  it('loads the project being edited, removes a gallery image and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/projects/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/3/edit', '/storefront/projects/:id/edit')

    expect((wrapper.get('[data-testid="project-form-slug"]').element as HTMLInputElement).value).toBe('site')
    expect((wrapper.get('[data-testid="project-form-title"]').element as HTMLInputElement).value).toBe('Site')
    expect((wrapper.get('[data-testid="project-form-image-1-url"]').element as HTMLInputElement).value).toBe('/img/b.png')

    await wrapper.get('[data-testid="project-form-image-delete-0"]').trigger('click')
    expect(wrapper.find('[data-testid="project-form-image-1-url"]').exists()).toBe(false)
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/projects/3')
    })
    expect(bodyOf(mock, 'PUT').imageUrls).toEqual(['/img/b.png'])
  })

  it('says when the project being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/99/edit', '/storefront/projects/:id/edit')

    expect(wrapper.get('[data-testid="project-form-load-error"]').text()).toBe('Entry #99 was not found.')
  })

  it('shows the API sentence and the slug message when saving is refused', async () => {
    stubRouteTable({
      ...routes,
      'POST /admin/projects': () => json({ type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail: 'Check the form.', errors: { slug: ['Slug is taken.'] } }, 400),
    })
    const { wrapper, router } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="project-form-error"]').text()).toBe('Check the form.')
    })
    expect(wrapper.text()).toContain('Slug is taken.')
    expect(router.currentRoute.value.path).toBe('/storefront/projects/create')
  })

  it('shows a plain refusal sentence', async () => {
    stubRouteTable({ ...routes, 'POST /admin/projects': () => problem('Not allowed.', 403) })
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="project-form-error"]').text()).toBe('Not allowed.')
    })
  })

  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
      for (const field of section.fields) {
        expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
      }
    }
  })

  it('keeps the fields of its schema as placeholders while the entry loads', async () => {
    stubRouteTable({ 'GET /admin/projects': () => new Promise<Response>(() => undefined) })
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/3/edit', '/storefront/projects/:id/edit')

    expect(wrapper.findAll('[data-testid="ui-form-section-general"][aria-busy="true"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="project-form-title"]').exists()).toBe(false)
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(ProjectFormView, {}, '/storefront/projects/create', '/storefront/projects/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="project-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="project-form-title"]').setValue('Written')

    expect(wrapper.find('[data-testid="project-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="project-form-tab-ru-empty"]').exists()).toBe(true)
  })
})
