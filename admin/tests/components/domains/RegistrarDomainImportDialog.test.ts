/**
 * Mount tests for RegistrarDomainImportDialog: only a registrar that lists its domains is offered, choosing one lists its
 * domains with what the panel already has, the import button needs a client and a domain the registrar gives an expiry for,
 * an import posts the client and the name and tells the parent, the pages move by cursor, and a refusal stays on screen.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import RegistrarDomainImportDialog from '@/components/domains/RegistrarDomainImportDialog.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { UiSelectOption } from '@/utils/ui/select'
import { createTestI18n } from '../../helpers/i18n'
import { callOf, json, problem } from '../../helpers/fetch'
import { stubRoutes } from '../../helpers/routes'

/** The import call. */
const IMPORT_LINE = 'POST /api/domains/registrar/registrar-one/import'

/** The installed plugins: two registrars, one of them listing its domains, and a payment plugin. */
const plugins = [
  { id: 'registrar-one', name: 'Registrar One', type: 'registrar', isLoaded: true, status: 'Loaded', capabilities: ['registrarOperations', 'registrarDomainList'] },
  { id: 'registrar-two', name: 'Registrar Two', type: 'registrar', isLoaded: true, status: 'Loaded', capabilities: ['registrarOperations'] },
  { id: 'a-gateway', name: 'A Gateway', type: 'payment', isLoaded: true, status: 'Loaded', capabilities: [] },
]

/** One client the picker offers. */
const clientRow = { id: 9, firstName: 'Grace', lastName: 'Hopper', companyName: null, email: 'grace@example.test', status: 'Active' }

/** Everything the dialog reads while it opens. */
const lookups = {
  'GET /api/admin/plugins': () => json(plugins),
  'GET /api/clients': () => json({ items: [clientRow], totalCount: 1 }),
}

/** The first page: one importable domain, one the panel has, one with no expiry; another page follows. */
const firstPage = {
  domains: [
    { name: 'free.example.com', expiresAt: '2027-03-01T00:00:00Z', inPanel: false },
    { name: 'held.example.com', expiresAt: '2027-04-01T00:00:00Z', inPanel: true },
    { name: 'undated.example.com', expiresAt: null, inPanel: false },
  ],
  nextCursor: 'c-2',
}

/** The second and last page. */
const secondPage = { domains: [{ name: 'later.example.com', expiresAt: '2027-05-01T00:00:00Z', inPanel: false }], nextCursor: null }

/** The registrar's list, by cursor. */
const pages = {
  'GET /api/domains/registrar/registrar-one?cursor=c-2': () => json(secondPage),
  'GET /api/domains/registrar/registrar-one': () => json(firstPage),
}

/** The Pinia the dialog and the test share. */
let pinia: ReturnType<typeof createPinia>

/**
 * Mounts the dialog and waits for what it loads on open.
 *
 * @returns The wrapper.
 */
const mountDialog = async (): Promise<VueWrapper> => {
  const wrapper = mount(RegistrarDomainImportDialog, { global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } } })
  await flushPromises()
  return wrapper
}

/**
 * Finds the registrar select among the dialog's selects.
 *
 * @param wrapper - The mounted dialog.
 * @returns The registrar select; throws when the dialog has none.
 */
const registrarSelect = (wrapper: VueWrapper): VueWrapper => {
  const select = wrapper.findAllComponents(UiSelect).find(candidate => candidate.attributes('data-testid') === 'registrar-import-registrar')
  if (select === undefined) {
    throw new Error('No registrar select')
  }
  return select
}

/**
 * Chooses the registrar in its select.
 *
 * @param wrapper - The mounted dialog.
 * @param id - The plugin id to choose.
 * @returns Promise resolving once the list has loaded.
 */
const chooseRegistrar = async (wrapper: VueWrapper, id: string): Promise<void> => {
  registrarSelect(wrapper).vm.$emit('update:modelValue', id)
  await flushPromises()
}

/**
 * Chooses the client in the picker.
 *
 * @param wrapper - The mounted dialog.
 * @returns Promise resolving once the choice has rendered.
 */
const chooseClient = async (wrapper: VueWrapper): Promise<void> => {
  wrapper.getComponent(UiClientSelect).vm.$emit('update:modelValue', '9')
  await flushPromises()
}

describe('RegistrarDomainImportDialog', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers only the registrars that list their domains', async () => {
    stubRoutes({ ...lookups, ...pages })
    const wrapper = await mountDialog()

    const options = (registrarSelect(wrapper).props() as unknown as { options: UiSelectOption<string>[] }).options

    expect(options).toEqual([{ value: 'registrar-one', label: 'Registrar One' }])
  })

  it('lists no domain until a registrar is chosen, then lists them with what the panel has', async () => {
    const mock = stubRoutes({ ...lookups, ...pages })
    const wrapper = await mountDialog()
    expect(wrapper.find('[data-testid="registrar-import-table"]').exists()).toBe(false)

    await chooseRegistrar(wrapper, 'registrar-one')

    expect(callOf(mock, mock.mock.calls.length - 1).url).toBe('/api/domains/registrar/registrar-one')
    expect(wrapper.get('[data-testid="registrar-import-domain-free.example.com"]').text()).toContain('Not in the panel')
    expect(wrapper.get('[data-testid="registrar-import-domain-held.example.com"]').text()).toContain('In the panel')
    expect(wrapper.get('[data-testid="registrar-import-domain-undated.example.com"]').text()).toContain('No expiry reported')
  })

  it('keeps every import button off until a client is chosen, and off for a domain that is in the panel or has no expiry', async () => {
    stubRoutes({ ...lookups, ...pages })
    const wrapper = await mountDialog()
    await chooseRegistrar(wrapper, 'registrar-one')

    expect(wrapper.get('[data-testid="registrar-import-row-free.example.com"]').attributes('disabled')).toBeDefined()

    await chooseClient(wrapper)

    expect(wrapper.get('[data-testid="registrar-import-row-free.example.com"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('[data-testid="registrar-import-row-held.example.com"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="registrar-import-row-undated.example.com"]').attributes('disabled')).toBeDefined()
  })

  it('imports a domain into the chosen client, marks it as in the panel and tells the parent', async () => {
    const mock = stubRoutes({ ...lookups, ...pages, [IMPORT_LINE]: () => new Response(null, { status: 201 }) })
    const wrapper = await mountDialog()
    await chooseRegistrar(wrapper, 'registrar-one')
    await chooseClient(wrapper)

    await wrapper.get('[data-testid="registrar-import-row-free.example.com"]').trigger('click')
    await flushPromises()

    const post = mock.mock.calls.findIndex(([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${String(url)}` === IMPORT_LINE)
    expect(callOf(mock, post).body).toEqual({ clientId: 9, domainName: 'free.example.com' })
    expect(wrapper.get('[data-testid="registrar-import-domain-free.example.com"]').text()).toContain('In the panel')
    expect(wrapper.emitted('imported')).toHaveLength(1)
  })

  it('shows the API refusal of an import and stays open', async () => {
    stubRoutes({ ...lookups, ...pages, [IMPORT_LINE]: () => problem('This domain is already registered in the system.', 409) })
    const wrapper = await mountDialog()
    await chooseRegistrar(wrapper, 'registrar-one')
    await chooseClient(wrapper)

    await wrapper.get('[data-testid="registrar-import-row-free.example.com"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="registrar-import-action-error"]').text()).toContain('This domain is already registered in the system.')
    expect(wrapper.emitted('imported')).toBeUndefined()
    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('pages by cursor: Next shows the following page, Previous the one before, and the buttons follow what exists', async () => {
    stubRoutes({ ...lookups, ...pages })
    const wrapper = await mountDialog()
    await chooseRegistrar(wrapper, 'registrar-one')
    expect(wrapper.get('[data-testid="registrar-import-previous"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="registrar-import-next"]').attributes('disabled')).toBeUndefined()

    await wrapper.get('[data-testid="registrar-import-next"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="registrar-import-domain-later.example.com"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="registrar-import-page"]').text()).toBe('Page 2')
    expect(wrapper.get('[data-testid="registrar-import-next"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="registrar-import-previous"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="registrar-import-domain-free.example.com"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="registrar-import-page"]').text()).toBe('Page 1')
  })

  it('shows the registrar refusal of the list instead of a table', async () => {
    stubRoutes({
      ...lookups,
      'GET /api/domains/registrar/registrar-one': () => problem('The registrar refused the saved credentials.', 503),
    })
    const wrapper = await mountDialog()

    await chooseRegistrar(wrapper, 'registrar-one')

    expect(wrapper.get('[data-testid="registrar-import-error"]').text()).toContain('The registrar refused the saved credentials.')
    expect(wrapper.find('[data-testid="registrar-import-table"]').exists()).toBe(false)
  })

  it('says so when the registrar account holds no domain', async () => {
    stubRoutes({ ...lookups, 'GET /api/domains/registrar/registrar-one': () => json({ domains: [], nextCursor: null }) })
    const wrapper = await mountDialog()

    await chooseRegistrar(wrapper, 'registrar-one')

    expect(wrapper.get('[data-testid="registrar-import-table"]').text()).toContain('This registrar account holds no domains')
  })
})
