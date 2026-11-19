/**
 * Smoke tests for PartnerFormView: create and edit modes, the logo upload, saving through the
 * store at network level, and the refusal and missing-entry messages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import PartnerFormView from '@/views/PartnerFormView.vue'
import schema from '@/views/PartnerFormView.schema'
import type { Partner, PartnerPayload } from '@/types/partner'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A stored partner. */
const stored: Partner = {
  id: 3,
  logoUrl: '/img/acme.png',
  url: 'https://acme.example.com',
  sortOrder: 2,
  isActive: false,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { name: 'Acme' } },
}

/** Answers for what the form loads. */
const routes = { 'GET /admin/partners': () => json([stored]) }

/**
 * Reads the JSON body of the first request with a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The verb to look for.
 * @returns The parsed payload.
 */
const bodyOf = (mock: ReturnType<typeof vi.fn>, method: string): PartnerPayload => {
  const call = mock.mock.calls.find((candidate) => (candidate[1] as RequestInit | undefined)?.method === method)
  return JSON.parse((call?.[1] as RequestInit | undefined)?.body as string) as PartnerPayload
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PartnerFormView', () => {
  it('opens an empty create form with the language tabs', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create partner')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create partner')
    expect(wrapper.find('[role="tablist"]').exists()).toBe(true)
  })

  it('saves a new partner with trimmed text, then returns to the list', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/partners': () => json(9) })
    const { wrapper, router } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')

    await wrapper.get('[data-testid="partner-form-name"]').setValue('  Globex ')
    await wrapper.get('[data-testid="partner-form-url"]').setValue(' https://globex.example.com ')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/partners')
    })

    expect(bodyOf(mock, 'POST')).toEqual({ logoUrl: '', url: 'https://globex.example.com', sortOrder: 0, isActive: true, translations: { en: { name: 'Globex' } } })
  })

  it('loads the partner being edited and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/partners/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/3/edit', '/storefront/partners/:id/edit')

    expect((wrapper.get('[data-testid="partner-form-name"]').element as HTMLInputElement).value).toBe('Acme')
    expect((wrapper.get('[data-testid="partner-form-url"]').element as HTMLInputElement).value).toBe('https://acme.example.com')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Save Changes')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/partners/3')
    })
    expect(bodyOf(mock, 'PUT')).toMatchObject({ isActive: false, sortOrder: 2, translations: { en: { name: 'Acme' } } })
  })

  it('says when the partner being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/99/edit', '/storefront/partners/:id/edit')

    expect(wrapper.get('[data-testid="partner-form-load-error"]').text()).toBe('Entry #99 was not found.')
  })

  it('shows the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ ...routes, 'POST /admin/partners': () => problem('A partner needs a logo.', 400) })
    const { wrapper, router } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="partner-form-error"]').text()).toBe('A partner needs a logo.')
    })
    expect(router.currentRoute.value.path).toBe('/storefront/partners/create')
  })

  it('stages the uploaded logo URL and previews it', async () => {
    stubRouteTable({ ...routes, 'POST /admin/storefront/upload-image': () => json({ url: '/img/logo.png' }) })
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')
    const input = wrapper.get('[data-testid="partner-form-logo-file"]')
    Object.defineProperty(input.element, 'files', { value: [new File(['x'], 'a.png')], configurable: true })

    await input.trigger('change')

    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="partner-form-logo-url"]').element as HTMLInputElement).value).toBe('/img/logo.png')
    })
    expect(wrapper.find('[data-testid="partner-form-logo-preview"]').exists()).toBe(true)
  })

  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')

    for (const section of schema.columns.flat()) {
      expect(wrapper.find(`[data-testid="ui-form-section-${section.key}"]`).exists()).toBe(true)
      for (const field of section.fields) {
        expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
      }
    }
  })

  it('keeps the fields of its schema as placeholders while the entry loads', async () => {
    stubRouteTable({ 'GET /admin/partners': () => new Promise<Response>(() => undefined) })
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/3/edit', '/storefront/partners/:id/edit')

    expect(wrapper.findAll('[data-testid="ui-form-section-general"][aria-busy="true"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="partner-form-name"]').exists()).toBe(false)
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(PartnerFormView, {}, '/storefront/partners/create', '/storefront/partners/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="partner-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="partner-form-name"]').setValue('Written')

    expect(wrapper.find('[data-testid="partner-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="partner-form-tab-ru-empty"]').exists()).toBe(true)
  })
})
