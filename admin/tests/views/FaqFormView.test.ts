/**
 * Smoke tests for FaqFormView: create and edit modes, the locale tabs, the category, saving through
 * the store at network level, and the refusal and missing-entry messages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import FaqFormView from '@/views/FaqFormView.vue'
import type { Faq, FaqPayload } from '@/types/faq'
import { FaqCategory } from '@/types/storefrontenums'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import schema from '@/views/FaqFormView.schema'

/** A stored entry with English and Armenian text. */
const stored: Faq = {
  id: 3,
  category: FaqCategory.Products,
  sortOrder: 2,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { question: 'Why?', answer: 'Because.' }, hy: { question: 'Ինչու՞', answer: 'Որովհետև։' } },
}

/** Answers for what the form loads. */
const routes = { 'GET /admin/faqs': () => json([stored]) }

/**
 * Reads the JSON body of the first request with a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The verb to look for.
 * @returns The parsed payload.
 */
const bodyOf = (mock: ReturnType<typeof vi.fn>, method: string): FaqPayload => {
  const call = mock.mock.calls.find((candidate) => (candidate[1] as RequestInit | undefined)?.method === method)
  return JSON.parse((call?.[1] as RequestInit | undefined)?.body as string) as FaqPayload
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FaqFormView', () => {
  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    const sections = schema.columns.flat()
    expect(wrapper.findAll('[data-testid^="ui-form-section-"]')).toHaveLength(sections.length)
    expect(wrapper.findAll('[data-testid^="ui-form-field-"]')).toHaveLength(sections.flatMap((section) => section.fields).length)
  })

  it('keeps the fields as placeholders while the entry being edited loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/3/edit', '/storefront/faqs/:id/edit')

    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.find('[data-testid="faq-form-active"]').exists()).toBe(false)
  })

  it('opens an empty create form with the General category', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create question')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create question')
    expect(wrapper.get('[data-testid="faq-form-category"]').text()).toContain('General')
  })

  it('saves a new entry with only the locales that have a question, then returns to the list', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/faqs': () => json(9) })
    const { wrapper, router } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    await wrapper.get('[data-testid="faq-form-question"]').setValue(' How? ')
    await wrapper.get('[data-testid="faq-form-answer"]').setValue(' Like this. ')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/faqs')
    })

    expect(bodyOf(mock, 'POST')).toEqual({
      category: FaqCategory.General,
      sortOrder: 0,
      isActive: true,
      translations: { en: { question: 'How?', answer: 'Like this.' } },
    })
  })

  it('keeps each locale tab separate', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    await wrapper.get('[data-testid="faq-form-question"]').setValue('English question')
    await wrapper.get('[data-testid="faq-form-tab-hy"]').trigger('click')
    expect((wrapper.get('[data-testid="faq-form-question"]').element as HTMLInputElement).value).toBe('')

    await wrapper.get('[data-testid="faq-form-tab-en"]').trigger('click')
    expect((wrapper.get('[data-testid="faq-form-question"]').element as HTMLInputElement).value).toBe('English question')
  })

  it('loads the entry being edited and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/faqs/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/3/edit', '/storefront/faqs/:id/edit')

    expect((wrapper.get('[data-testid="faq-form-question"]').element as HTMLInputElement).value).toBe('Why?')
    expect(wrapper.get('[data-testid="faq-form-category"]').text()).toContain('Products')
    await wrapper.get('[data-testid="faq-form-tab-hy"]').trigger('click')
    expect((wrapper.get('[data-testid="faq-form-answer"]').element as HTMLTextAreaElement).value).toBe('Որովհետև։')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/faqs/3')
    })
    expect(bodyOf(mock, 'PUT')).toMatchObject({ category: FaqCategory.Products, sortOrder: 2 })
  })

  it('says when the entry being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/99/edit', '/storefront/faqs/:id/edit')

    expect(wrapper.get('[data-testid="faq-form-load-error"]').text()).toBe('Entry #99 was not found.')
  })

  it('shows the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ ...routes, 'POST /admin/faqs': () => problem('A question needs text.', 400) })
    const { wrapper, router } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="faq-form-error"]').text()).toBe('A question needs text.')
    })
    expect(router.currentRoute.value.path).toBe('/storefront/faqs/create')
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(FaqFormView, {}, '/storefront/faqs/create', '/storefront/faqs/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="faq-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="faq-form-question"]').setValue('Written')

    expect(wrapper.find('[data-testid="faq-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="faq-form-tab-ru-empty"]').exists()).toBe(true)
  })
})
