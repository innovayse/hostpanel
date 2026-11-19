/**
 * Smoke tests for LocationFormView: create and edit modes, the upper-cased country code, the
 * per-locale city and latency note, saving through the store at network level, and the refusal and
 * missing-entry messages.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import LocationFormView from '@/views/LocationFormView.vue'
import type { ServerLocation, ServerLocationPayload } from '@/types/serverlocation'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import schema from '@/views/LocationFormView.schema'

/** A stored location. */
const stored: ServerLocation = {
  id: 3,
  countryCode: 'DE',
  sortOrder: 2,
  isActive: false,
  translations: { en: { name: 'Frankfurt', city: 'Frankfurt am Main', latencyNote: null } },
}

/** Answers for what the form loads. */
const routes = { 'GET /admin/locations': () => json([stored]) }

/**
 * Reads the JSON body of the first request with a verb.
 *
 * @param mock - The fetch mock.
 * @param method - The verb to look for.
 * @returns The parsed payload.
 */
const bodyOf = (mock: ReturnType<typeof vi.fn>, method: string): ServerLocationPayload => {
  const call = mock.mock.calls.find((candidate) => (candidate[1] as RequestInit | undefined)?.method === method)
  return JSON.parse((call?.[1] as RequestInit | undefined)?.body as string) as ServerLocationPayload
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('LocationFormView', () => {
  it('draws every section and field of its schema', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/create', '/storefront/locations/create')

    const sections = schema.columns.flat()
    expect(wrapper.findAll('[data-testid^="ui-form-section-"]')).toHaveLength(sections.length)
    expect(wrapper.findAll('[data-testid^="ui-form-field-"]')).toHaveLength(sections.flatMap((section) => section.fields).length)
  })

  it('keeps the fields as placeholders while the entry being edited loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/3/edit', '/storefront/locations/:id/edit')

    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.find('[data-testid="location-form-active"]').exists()).toBe(false)
  })

  it('opens an empty create form with the language tabs', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/create', '/storefront/locations/create')

    expect(wrapper.find('[data-testid="ui-breadcrumb"]').exists()).toBe(false)
    expect(wrapper.findAll('h1')).toHaveLength(1)
    expect(wrapper.get('h1').text()).toBe('Create location')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create location')
    expect(wrapper.find('[role="tablist"]').exists()).toBe(true)
  })

  it('saves a new location with an upper-case code and trimmed text, then returns to the list', async () => {
    const mock = stubRouteTable({ ...routes, 'POST /admin/locations': () => json(9) })
    const { wrapper, router } = await mountScreen(LocationFormView, {}, '/storefront/locations/create', '/storefront/locations/create')

    await wrapper.get('[data-testid="location-form-country"]').setValue('am')
    await wrapper.get('[data-testid="location-form-name"]').setValue('  Yerevan ')
    await wrapper.get('[data-testid="location-form-city"]').setValue(' Yerevan city ')
    await wrapper.get('[data-testid="location-form-latency-note"]').setValue(' 10 ms ')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/locations')
    })

    expect(bodyOf(mock, 'POST')).toEqual({
      countryCode: 'AM',
      sortOrder: 0,
      isActive: true,
      translations: { en: { name: 'Yerevan', city: 'Yerevan city', latencyNote: '10 ms' } },
    })
  })

  it('loads the location being edited and updates it with PUT', async () => {
    const mock = stubRouteTable({ ...routes, 'PUT /admin/locations/3': () => new Response(null, { status: 204 }) })
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/3/edit', '/storefront/locations/:id/edit')

    expect((wrapper.get('[data-testid="location-form-country"]').element as HTMLInputElement).value).toBe('DE')
    expect((wrapper.get('[data-testid="location-form-name"]').element as HTMLInputElement).value).toBe('Frankfurt')
    expect((wrapper.get('[data-testid="location-form-city"]').element as HTMLInputElement).value).toBe('Frankfurt am Main')
    expect((wrapper.get('[data-testid="location-form-latency-note"]').element as HTMLInputElement).value).toBe('')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Save Changes')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/locations/3')
    })
    expect(bodyOf(mock, 'PUT')).toMatchObject({ countryCode: 'DE', isActive: false, sortOrder: 2, translations: { en: { name: 'Frankfurt' } } })
  })

  it('says when the location being edited does not exist', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/99/edit', '/storefront/locations/:id/edit')

    expect(wrapper.get('[data-testid="location-form-load-error"]').text()).toBe('Entry #99 was not found.')
  })

  it('shows the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ ...routes, 'POST /admin/locations': () => problem('A location needs a country code.', 400) })
    const { wrapper, router } = await mountScreen(LocationFormView, {}, '/storefront/locations/create', '/storefront/locations/create')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="location-form-error"]').text()).toBe('A location needs a country code.')
    })
    expect(router.currentRoute.value.path).toBe('/storefront/locations/create')
  })

  it('marks the language tabs with no text and drops the mark of a language once it is written', async () => {
    stubRouteTable(routes)
    const { wrapper } = await mountScreen(LocationFormView, {}, '/storefront/locations/create', '/storefront/locations/create')

    for (const locale of ['en', 'ru', 'hy']) {
      expect(wrapper.find(`[data-testid="location-form-tab-${locale}-empty"]`).exists()).toBe(true)
    }

    await wrapper.get('[data-testid="location-form-name"]').setValue('Written')

    expect(wrapper.find('[data-testid="location-form-tab-en-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="location-form-tab-ru-empty"]').exists()).toBe(true)
  })
})
