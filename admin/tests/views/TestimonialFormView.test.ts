/**
 * Smoke tests for TestimonialFormView: create and edit modes, the locale tabs, the avatar upload,
 * saving through the store at network level, and the refusal and missing-entry messages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import TestimonialFormView from '@/views/TestimonialFormView.vue'
import schema from '@/views/TestimonialFormView.schema'
import type { Testimonial, TestimonialPayload } from '@/types/testimonial'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A stored testimonial with English and Russian quotes. */
const stored: Testimonial = {
  id: 3,
  name: 'Ann',
  company: 'Acme',
  avatarUrl: '/img/ann.png',
  rating: 4,
  sortOrder: 2,
  isActive: true,
  publishedAt: '2026-03-10T00:00:00+00:00',
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { role: 'CTO', quote: 'Great' }, ru: { role: '', quote: 'Отлично' } },
}

/** Answers for what the form loads. */
const routes = { 'GET /admin/testimonials': () => json([stored]) }

/**
 * Reads the JSON body of the first request with a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The verb to look for.
 * @returns The parsed payload.
 */
const bodyOf = (mock: ReturnType<typeof vi.fn>, method: string): TestimonialPayload => {
  const call = mock.mock.calls.find((candidate) => (candidate[1] as RequestInit | undefined)?.method === method)
  return JSON.parse((call?.[1] as RequestInit | undefined)?.body as string) as TestimonialPayload
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TestimonialFormView', () => {
  it('opens an empty create form with the trail and the create button', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create testimonial')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create testimonial')
    expect((wrapper.get('[data-testid="testimonial-form-name"]').element as HTMLInputElement).value).toBe('')
  })

  it('saves a new testimonial with only the locales that have a quote, then returns to the list', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/testimonials': () => json(9) })
    const { wrapper, router } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    await wrapper.get('[data-testid="testimonial-form-name"]').setValue('  Bob  ')
    await wrapper.get('[data-testid="testimonial-form-quote"]').setValue(' Fast ')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/testimonials')
    })

    expect(bodyOf(mock, 'POST')).toMatchObject({ name: 'Bob', rating: 5, isActive: true, translations: { en: { role: '', quote: 'Fast' } } })
    expect(Object.keys(bodyOf(mock, 'POST').translations)).toEqual(['en'])
  })

  it('sends no date when the date field is left empty', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/testimonials': () => json(9) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    await wrapper.get('[data-testid="testimonial-form-name"]').setValue('Bob')
    await wrapper.get('[data-testid="testimonial-form-quote"]').setValue('Fast')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('POST /admin/testimonials')
    })

    expect(bodyOf(mock, 'POST').publishedAt).toBeNull()
  })

  it('sends the typed date as a UTC instant', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/testimonials': () => json(9) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    await wrapper.get('[data-testid="testimonial-form-name"]').setValue('Bob')
    await wrapper.get('[data-testid="testimonial-form-quote"]').setValue('Fast')
    await wrapper.get('[data-testid="date-picker-input"]').setValue('02/05/2026')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('POST /admin/testimonials')
    })

    expect(bodyOf(mock, 'POST').publishedAt).toBe('2026-05-02T00:00:00Z')
  })

  it('shows the stored date when editing and sends it back unchanged', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/testimonials/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/3/edit', '/storefront/testimonials/:id/edit')

    expect((wrapper.get('[data-testid="date-picker-input"]').element as HTMLInputElement).value).toBe('10/03/2026')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/testimonials/3')
    })
    expect(bodyOf(mock, 'PUT').publishedAt).toBe('2026-03-10T00:00:00Z')
  })

  it('keeps each locale tab separate', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    await wrapper.get('[data-testid="testimonial-form-quote"]').setValue('English quote')
    await wrapper.get('[data-testid="testimonial-form-tab-hy"]').trigger('click')
    expect((wrapper.get('[data-testid="testimonial-form-quote"]').element as HTMLTextAreaElement).value).toBe('')

    await wrapper.get('[data-testid="testimonial-form-tab-en"]').trigger('click')
    expect((wrapper.get('[data-testid="testimonial-form-quote"]').element as HTMLTextAreaElement).value).toBe('English quote')
  })

  it('loads the testimonial being edited and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/testimonials/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/3/edit', '/storefront/testimonials/:id/edit')

    expect((wrapper.get('[data-testid="testimonial-form-name"]').element as HTMLInputElement).value).toBe('Ann')
    expect((wrapper.get('[data-testid="testimonial-form-quote"]').element as HTMLTextAreaElement).value).toBe('Great')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Save')
    await wrapper.get('[data-testid="testimonial-form-tab-ru"]').trigger('click')
    expect((wrapper.get('[data-testid="testimonial-form-quote"]').element as HTMLTextAreaElement).value).toBe('Отлично')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/testimonials/3')
    })
    expect(bodyOf(mock, 'PUT').translations).toEqual({ en: { role: 'CTO', quote: 'Great' }, ru: { role: '', quote: 'Отлично' } })
  })

  it('says when the testimonial being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/99/edit', '/storefront/testimonials/:id/edit')

    expect(wrapper.get('[data-testid="testimonial-form-load-error"]').text()).toBe('Entry #99 was not found.')
  })

  it('shows the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ ...routes, 'POST /admin/testimonials': () => problem('A testimonial needs a name.', 400) })
    const { wrapper, router } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="testimonial-form-error"]').text()).toBe('A testimonial needs a name.')
    })
    expect(router.currentRoute.value.path).toBe('/storefront/testimonials/create')
  })

  it('stages the uploaded avatar URL and previews it', async () => {
    stubRouteTable({ ...routes, 'POST /admin/storefront/upload-image': () => json({ url: '/img/up.png' }) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')
    const input = wrapper.get('[data-testid="testimonial-form-avatar-file"]')
    Object.defineProperty(input.element, 'files', { value: [new File(['x'], 'a.png')], configurable: true })

    await input.trigger('change')

    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="testimonial-form-avatar-url"]').element as HTMLInputElement).value).toBe('/img/up.png')
    })
    expect(wrapper.find('[data-testid="testimonial-form-avatar-preview"]').exists()).toBe(true)
  })

  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
      for (const field of section.fields) {
        expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
      }
    }
  })

  it('keeps the fields of its schema as placeholders while the entry loads', async () => {
    stubRouteTable({ 'GET /admin/testimonials': () => new Promise<Response>(() => undefined) })
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/3/edit', '/storefront/testimonials/:id/edit')

    expect(wrapper.findAll('[data-testid="ui-form-section-general"][aria-busy="true"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="testimonial-form-name"]').exists()).toBe(false)
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(TestimonialFormView, {}, '/storefront/testimonials/create', '/storefront/testimonials/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="testimonial-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="testimonial-form-quote"]').setValue('Written')

    expect(wrapper.find('[data-testid="testimonial-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="testimonial-form-tab-ru-empty"]').exists()).toBe(true)
  })
})
