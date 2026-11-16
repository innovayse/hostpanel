/**
 * Tests for SystemSettingsView: the tab strip lives in the address, each tab renders its own cards and its
 * own sticky bar, a save goes through the store (network level), a refusal shows the API's own sentence, and
 * the first load keeps the tab layout.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import SystemSettingsView from '@/views/SystemSettingsView.vue'
import { useVersionStore } from '@/stores/version'
import type { Setting } from '@/types/setting'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** Seeded rows: one identity row (General), one branding row (Appearance), one raw row only Advanced lists. */
const stored: Setting[] = [
  { id: 1, key: 'portal.site.name', value: 'Acme' },
  { id: 11, key: 'portal.logo', value: '', description: 'Logo' },
  { id: 90, key: 'general.timezone', value: 'UTC', description: 'Default time zone' },
]

/** Path the screen is mounted at, which the tab query is appended to. */
const SCREEN_PATH = '/settings/general'

/** Address value of the General tab, the default. */
const GENERAL = 'general'

/**
 * Finds the panel of one tab; every panel stays mounted, so the tests scope to it.
 *
 * @param wrapper - The mounted screen.
 * @param tab - The tab's address value.
 * @returns The panel's wrapper.
 */
const panelOf = (wrapper: VueWrapper, tab: string) => wrapper.get(`[data-testid="settings-panel-${tab}"]`)

/**
 * Whether a panel is the one on show (the others are hidden with `v-show`).
 *
 * @param wrapper - The mounted screen.
 * @param tab - The tab's address value.
 * @returns True when the panel is not hidden.
 */
const isShown = (wrapper: VueWrapper, tab: string): boolean => !(panelOf(wrapper, tab).attributes('style') ?? '').includes('display: none')

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SystemSettingsView', () => {
  it('renders the four tabs, selects General by default and shows only its cards', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    expect(wrapper.get('h1').text()).toBe('System Settings')
    expect(wrapper.get('[role="tablist"]').attributes('aria-label')).toBe('Settings sections')
    expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['General', 'Appearance', 'Integrations', 'Advanced'])
    expect(wrapper.get('[data-testid="settings-tab-general"]').attributes('aria-selected')).toBe('true')
    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme')
    expect(isShown(wrapper, 'general')).toBe(true)
    expect(isShown(wrapper, 'appearance')).toBe(false)
    expect(isShown(wrapper, 'advanced')).toBe(false)
  })

  it('opens the Advanced tab from the address and lists the raw rows', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=advanced`)

    expect(wrapper.get('[data-testid="settings-tab-advanced"]').attributes('aria-selected')).toBe('true')
    expect(wrapper.text()).toContain('Advanced settings')
    expect(wrapper.get('[data-testid="setting-row-90"]').text()).toContain('general.timezone')
    expect(wrapper.get('[data-testid="setting-row-90"]').text()).toContain('Default time zone')
    expect(isShown(wrapper, 'general')).toBe(false)
  })

  it('falls back to General for an unknown tab', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=nope`)

    expect(wrapper.get('[data-testid="settings-tab-general"]').attributes('aria-selected')).toBe('true')
    expect(isShown(wrapper, 'general')).toBe(true)
  })

  it('writes the chosen tab into the address and drops the key for General', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper, router } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await wrapper.get('[data-testid="settings-tab-appearance"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.tab).toBe('appearance')
    expect(isShown(wrapper, 'appearance')).toBe(true)
    expect(isShown(wrapper, 'general')).toBe(false)

    await wrapper.get('[data-testid="settings-tab-general"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.tab).toBeUndefined()
  })

  it('moves between tabs with the arrow keys', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper, router } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await wrapper.get('[data-testid="settings-tab-general"]').trigger('keydown', { key: 'ArrowRight' })
    await flushPromises()

    expect(router.currentRoute.value.query.tab).toBe('appearance')
  })

  it('shows the installed version under the title', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)
    useVersionStore().version = '1.4.0'
    await flushPromises()

    expect(wrapper.get('[data-testid="settings-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('shows no version line when the version cannot be loaded', async () => {
    const mock = stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'GET /admin/version': () => problem('No access.', 403),
    })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)
    // The layout starts this load in the app; here the test starts it and waits for the refusal.
    await useVersionStore().ensureLoaded()
    await flushPromises()

    expect(requestsOf(mock)).toContain('GET /admin/version')
    expect(wrapper.find('[data-testid="settings-version"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="system-settings-error"]').exists()).toBe(false)
  })

  it('counts the unsaved edits of a tab in its bar and saves them', async () => {
    const mock = stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-status"]').text()).toBe('No unsaved changes')
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-status"]').text()).toBe('1 unsaved change')

    await panelOf(wrapper, GENERAL).get('[data-testid="settings-form-save"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/settings/1')
    })
    const put = mock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({ value: 'Acme Cloud' })
  })

  it('drops the unsaved edits of a tab from its bar', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    await panelOf(wrapper, GENERAL).get('[data-testid="settings-form-cancel"]').trigger('click')

    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme')
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-status"]').text()).toBe('No unsaved changes')
  })

  it('keeps the bar of another tab idle while one tab has an edit', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-status"]').text()).toBe('1 unsaved change')

    await wrapper.get('[data-testid="settings-tab-advanced"]').trigger('click')
    await flushPromises()

    expect(panelOf(wrapper, 'advanced').get('[data-testid="settings-form-status"]').text()).toBe('No unsaved changes')
    expect(panelOf(wrapper, 'advanced').get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
  })

  it('keeps the edit and its bar count of a tab after switching away and back', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    await wrapper.get('[data-testid="settings-tab-appearance"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="settings-tab-general"]').trigger('click')
    await flushPromises()

    expect(isShown(wrapper, 'general')).toBe(true)
    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme Cloud')
    expect(panelOf(wrapper, GENERAL).get('[data-testid="settings-form-status"]').text()).toBe('1 unsaved change')
    expect(panelOf(wrapper, 'appearance').get('[data-testid="settings-form-status"]').text()).toBe('No unsaved changes')
  })

  it('saves an Advanced row from the tab bar and shows its outcome', async () => {
    const mock = stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/90': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=advanced`)

    await wrapper.get('[data-testid="setting-value-90"]').setValue('Asia/Yerevan')
    expect(panelOf(wrapper, 'advanced').get('[data-testid="settings-form-status"]').text()).toBe('1 unsaved change')
    await panelOf(wrapper, 'advanced').get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/settings/90')
    })
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="setting-outcome-90"]').text()).toBe('Changes saved successfully.')
    })
  })

  it('keeps the typed text and shows the API sentence when a save is refused', async () => {
    stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/90': () => problem('Not a known time zone.', 400),
    })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=advanced`)

    await wrapper.get('[data-testid="setting-value-90"]').setValue('nope')
    await panelOf(wrapper, 'advanced').get('[data-testid="settings-form-save"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="setting-outcome-90"]').text()).toBe('Not a known time zone.')
    })
    expect((wrapper.get('[data-testid="setting-value-90"]').element as HTMLInputElement).value).toBe('nope')
  })

  it('keeps the tab strip and draws the real cards with placeholder fields on the first load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    expect(wrapper.findAll('[role="tab"]')).toHaveLength(4)
    expect(wrapper.find('[data-testid="ui-card-skeleton"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-loading"]').length).toBeGreaterThan(0)
    expect(wrapper.find('[data-testid="setting-not-seeded"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="site-identity-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="settings-form-save"]').exists()).toBe(false)
  })

  it('draws the Advanced table as a placeholder on the first load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=advanced`)

    expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Key')
  })

  it('says so when the Advanced tab has no rows', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json([]) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, `${SCREEN_PATH}?tab=advanced`)

    expect(wrapper.text()).toContain('No settings found.')
  })

  it('shows the API sentence when the list cannot be loaded', async () => {
    stubRouteTable({ 'GET /admin/settings': () => problem('The settings store is down.', 503) })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    expect(wrapper.get('[data-testid="system-settings-error"]').text()).toBe('The settings store is down.')
    expect(wrapper.findAll('[role="tab"]')).toHaveLength(4)
  })

  it('loads the settings again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      'GET /admin/settings': () => {
        calls += 1
        return calls === 1 ? problem('The settings store is down.', 503) : json(stored)
      },
    })
    const { wrapper } = await mountScreen(SystemSettingsView, {}, SCREEN_PATH)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="system-settings-error"]').text()).toBe('The settings store is down.')
    })
    await wrapper.get('[data-testid="system-settings-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme')
    })
  })
})
