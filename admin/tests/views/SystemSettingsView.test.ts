/**
 * Smoke tests for SystemSettingsView: the cards and the key-value table render from the loaded
 * settings, a row is edited and saved through the store (network level), and a refusal shows the
 * API's own sentence.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import SystemSettingsView from '@/views/SystemSettingsView.vue'
import type { Setting } from '@/types/setting'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** Seeded rows: one identity row and one plain row that only the table edits. */
const stored: Setting[] = [
  { id: 1, key: 'portal.site.name', value: 'Acme' },
  { id: 21, key: 'portal.contact.email', value: 'old@example.com', description: 'Contact address' },
]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SystemSettingsView', () => {
  it('renders the cards and a table row per setting once loaded', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect(wrapper.get('h1').text()).toBe('System Settings')
    expect((wrapper.get('[data-testid="site-identity-name"]').element as HTMLInputElement).value).toBe('Acme')
    expect(wrapper.get('[data-testid="setting-row-21"]').text()).toContain('portal.contact.email')
    expect(wrapper.get('[data-testid="setting-row-21"]').text()).toContain('Contact address')
  })

  it('shows the installed version under the title', async () => {
    stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'GET /admin/release-status': () => json({ installedVersion: '1.4.0', isUpgradeAvailable: false }),
    })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect(wrapper.get('[data-testid="settings-version"]').text()).toBe('Hostpanel 1.4.0')
  })

  it('shows no version line when the version cannot be loaded', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json(stored) })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect(wrapper.find('[data-testid="settings-version"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="system-settings-error"]').exists()).toBe(false)
  })

  it('draws the storefront card with the stored figure', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json([...stored, { id: 30, key: 'portal.stats.clients', value: '5,000+' }]) })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect((wrapper.get('[data-testid="storefront-stats-clients"]').element as HTMLInputElement).value).toBe('5,000+')
  })

  it('edits a table row and saves only that row', async () => {
    const mock = stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/21': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(SystemSettingsView)
    expect(wrapper.find('[data-testid="setting-save-21"]').exists()).toBe(false)

    await wrapper.get('[data-testid="setting-value-21"]').setValue('new@example.com')
    await wrapper.get('[data-testid="setting-save-21"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/settings/21')
    })

    const put = mock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'PUT')
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({ value: 'new@example.com' })
    expect(requestsOf(mock).filter((call) => call === 'GET /admin/settings')).toHaveLength(2)
    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="setting-outcome-21"]').text()).toBe('Changes saved successfully.')
    })
  })

  it('shows the outcome of a card save inside the card, with no page-level line', async () => {
    stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/1': () => problem('Name is too long.', 400),
    })
    const { wrapper } = await mountScreen(SystemSettingsView)

    await wrapper.get('[data-testid="site-identity-name"]').setValue('Acme Cloud')
    await wrapper.get('[data-testid="site-identity-save-name"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Name is too long.')
    })
    expect(wrapper.find('[data-testid="system-settings-feedback"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="setting-outcome-21"]').text()).toBe('')
  })

  it('keeps the typed text and shows the API sentence when a save is refused', async () => {
    stubRouteTable({
      'GET /admin/settings': () => json(stored),
      'PUT /admin/settings/21': () => problem('Not a valid e-mail address.', 400),
    })
    const { wrapper } = await mountScreen(SystemSettingsView)

    await wrapper.get('[data-testid="setting-value-21"]').setValue('nope')
    await wrapper.get('[data-testid="setting-save-21"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="setting-outcome-21"]').text()).toBe('Not a valid e-mail address.')
    })
    expect((wrapper.get('[data-testid="setting-value-21"]').element as HTMLInputElement).value).toBe('nope')
  })

  it('says so when there are no settings', async () => {
    stubRouteTable({ 'GET /admin/settings': () => json([]) })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect(wrapper.text()).toContain('No settings found.')
  })

  it('shows the API sentence when the list cannot be loaded', async () => {
    stubRouteTable({ 'GET /admin/settings': () => problem('The settings store is down.', 503) })
    const { wrapper } = await mountScreen(SystemSettingsView)

    expect(wrapper.get('[data-testid="system-settings-error"]').text()).toBe('The settings store is down.')
  })

  it('loads the settings again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      'GET /admin/settings': () => {
        calls += 1
        return calls === 1 ? problem('The settings store is down.', 503) : json(stored)
      },
    })
    const { wrapper } = await mountScreen(SystemSettingsView)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="system-settings-error"]').text()).toBe('The settings store is down.')
    })
    await wrapper.get('[data-testid="system-settings-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="setting-row-21"]').text()).toContain('portal.contact.email')
    })
  })
})
