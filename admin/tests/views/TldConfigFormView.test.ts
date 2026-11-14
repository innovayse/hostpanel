/**
 * Smoke tests for TldConfigFormView: create and edit modes, the TLD input filter, live symbols and
 * margins in the pricing tables, saving through the store at network level, and refusals.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import TldConfigFormView from '@/views/TldConfigFormView.vue'
import type { TldConfig } from '@/types/tldconfig'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { pickOption } from '../helpers/select'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** A stored configuration with a priced first period. */
const stored: TldConfig = {
  id: 5,
  tld: 'net',
  registrarModule: 'Namecheap',
  currency: 'USD',
  sellCurrency: 'EUR',
  isEnabled: false,
  sortOrder: 3,
  categories: ['tech', 'popular'],
  costs: { register: { '1': 10 }, transfer: {}, renew: {} },
  sells: { register: { '1': 12.5 }, transfer: {}, renew: {} },
  lastSyncedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
}

/** The create route. */
const CREATE = '/settings/tld-pricing/create'

/** The edit route pattern. */
const EDIT = '/settings/tld-pricing/:id/edit'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TldConfigFormView', () => {
  it('opens an empty create form with its trail and button', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)

    expect(wrapper.get('[data-testid="settings-breadcrumb"]').text()).toContain('Add TLD')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Create TLD')
    expect((wrapper.get('[data-testid="tld-config-form-tld"]').element as HTMLInputElement).value).toBe('')
  })

  it('keeps the TLD to lower-case letters, digits and hyphens', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)

    await wrapper.get('[data-testid="tld-config-form-tld"]').setValue('Co.M!-1')

    expect((wrapper.get('[data-testid="tld-config-form-tld"]').element as HTMLInputElement).value).toBe('com-1')
  })

  it('refuses to save without a TLD, with no request sent', async () => {
    const mock = stubRouteTable({})
    const { wrapper } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    expect(wrapper.get('[data-testid="tld-config-form-error"]').text()).toBe('TLD is required.')
    expect(requestsOf(mock)).toEqual([])
  })

  it('shows the currency symbols in the table headers as the currencies change', async () => {
    stubRouteTable({})
    const { wrapper } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)
    expect(wrapper.text()).toContain('Cost (֏)')
    expect(wrapper.text()).toContain('Sell ($)')

    await pickOption(wrapper, 'tld-config-form-sell-currency', 'EUR (€)')

    expect(wrapper.text()).toContain('Sell (€)')
  })

  it('creates a TLD with only the priced periods and returns to the list', async () => {
    const mock = stubRouteTable({
      'POST /admin/tld-configs': () => json(8),
    })
    const { wrapper, router } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)

    await wrapper.get('[data-testid="tld-config-form-tld"]').setValue('com')
    await wrapper.get('[data-testid="tld-config-form-categories"]').setValue('popular, business ,')
    await wrapper.get('[data-testid="tld-register-cost-1"] input').setValue('9.5')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/tld-pricing')
    })

    const post = mock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'POST')
    const body = JSON.parse((post?.[1] as RequestInit | undefined)?.body as string) as Record<string, unknown>
    expect(body).toMatchObject({
      tld: 'com',
      registrarModule: 'NameAm',
      currency: 'AMD',
      sellCurrency: 'USD',
      categories: ['popular', 'business'],
      costRegister: { '1': 9.5 },
      costTransfer: {},
    })
  })

  it('loads the configuration being edited, locks the TLD and updates with PUT', async () => {
    const mock = stubRouteTable({
      'GET /admin/tld-configs/5': () => json(stored),
      'PUT /admin/tld-configs/5': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TldConfigFormView, {}, '/settings/tld-pricing/5/edit', EDIT)
    const tld = wrapper.get('[data-testid="tld-config-form-tld"]')

    expect((tld.element as HTMLInputElement).value).toBe('net')
    expect(tld.attributes('disabled')).toBeDefined()
    expect((wrapper.get('[data-testid="tld-config-form-categories"]').element as HTMLInputElement).value).toBe('tech, popular')
    expect(wrapper.get('[data-testid="tld-register-margin-1"]').text()).toBe('+25.0%')
    expect(wrapper.get('[data-testid="settings-form-save"]').text()).toBe('Save Changes')

    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/tld-configs/5')
    })
  })

  it('shows the API sentence with a retry when the configuration cannot be loaded', async () => {
    let calls = 0
    stubRouteTable({
      'GET /admin/tld-configs/9': () => {
        calls += 1
        return calls === 1 ? problem('No such TLD.', 404) : json({ ...stored, id: 9 })
      },
    })
    const { wrapper } = await mountScreen(TldConfigFormView, {}, '/settings/tld-pricing/9/edit', EDIT)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-config-form-load-error"]').text()).toBe('No such TLD.')
    })
    expect(wrapper.find('[data-testid="tld-config-form-tld"]').exists()).toBe(false)

    await wrapper.get('[data-testid="tld-config-form-load-error-retry"]').trigger('click')
    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="tld-config-form-tld"]').element as HTMLInputElement).value).toBe('net')
    })
  })

  it('shows the API sentence when saving is refused and stays on the form', async () => {
    stubRouteTable({ 'POST /admin/tld-configs': () => problem('That TLD already exists.', 409) })
    const { wrapper, router } = await mountScreen(TldConfigFormView, {}, CREATE, CREATE)

    await wrapper.get('[data-testid="tld-config-form-tld"]').setValue('com')
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="tld-config-form-error"]').text()).toBe('That TLD already exists.')
    })
    expect(router.currentRoute.value.path).toBe(CREATE)
  })
})
