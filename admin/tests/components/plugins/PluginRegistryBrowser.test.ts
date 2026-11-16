/**
 * Tests for PluginRegistryBrowser: a shared plugin card per listing, Install for a plugin that is absent,
 * Update for one with a newer version, Details in place of Install for a paid plugin without a key, the
 * gear that opens the details page of an installed plugin, per card busy state, the staged and update
 * badges, the three column grid, the card placeholders while loading, the empty and error states, and the
 * page event.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginRegistryBrowser from '@/components/plugins/PluginRegistryBrowser.vue'
import { PluginCategory, PluginPricing } from '@/types/plugin'
import type { PluginRegistryItemDto } from '@/types/plugin'
import { mountScreen } from '../../helpers/mount'

/** A verified, free listing that is not installed. */
const fresh: PluginRegistryItemDto = {
  id: 'example-pay',
  name: 'Example Pay',
  author: 'Hostpanel',
  description: 'Card payments',
  iconUrl: null,
  category: PluginCategory.Payment,
  latestVersion: '2.0.0',
  sdkVersion: '3.0.0',
  verified: true,
  pricing: PluginPricing.Free,
  installed: false,
  installedVersion: null,
  updateAvailable: false,
}

/** An unverified, paid listing that is installed with a newer version on offer. */
const outdated: PluginRegistryItemDto = {
  ...fresh,
  id: 'example-registrar',
  name: 'Example Registrar',
  verified: false,
  pricing: PluginPricing.Paid,
  installed: true,
  installedVersion: '1.0.0',
  updateAvailable: true,
}

/** A listing that is installed and current. */
const current: PluginRegistryItemDto = {
  ...fresh,
  id: 'example-host',
  name: 'Example Host',
  installed: true,
  installedVersion: '2.0.0',
}

/**
 * Mounts the browser with a default page of the three listings.
 *
 * @param overrides - Props to change.
 * @returns The wrapper.
 */
const mountBrowser = async (overrides: Record<string, unknown> = {}) => {
  const { wrapper } = await mountScreen(PluginRegistryBrowser, {
    items: [fresh, outdated, current],
    total: 3,
    pageSize: 10,
    page: 1,
    loading: false,
    error: null,
    installingIds: [],
    ...overrides,
  })
  return wrapper
}

describe('PluginRegistryBrowser', () => {
  it('draws a shared card per listing with the verified mark only for a verified publisher', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').text()).toContain('Example Pay')
    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').text()).toContain('by Hostpanel')
    expect(wrapper.find('[data-testid="plugin-card-verified-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-card-verified-example-registrar"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-card-meta-example-registrar"]').text()).toContain('Paid')
    expect(wrapper.get('[data-testid="plugin-card-meta-example-pay"]').text()).toContain('Free')
  })

  it('links every card to its details page', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="plugin-card-link-example-pay"]').attributes('href')).toBe('/plugins/example-pay')
  })

  it('shows how many plugins match above the list', async () => {
    const wrapper = await mountBrowser({ total: 31 })

    expect(wrapper.get('[data-testid="plugin-registry-total"]').text()).toBe('31 plugins')
  })

  it('labels a private listing', async () => {
    const wrapper = await mountBrowser({ items: [{ ...fresh, pricing: PluginPricing.Private }] })

    expect(wrapper.get('[data-testid="plugin-card-meta-example-pay"]').text()).toContain('Private')
  })

  it('offers Install for a plugin that is not installed and emits its id and latest version', async () => {
    const wrapper = await mountBrowser()
    const button = wrapper.get('[data-testid="registry-install-example-pay"]')

    expect(button.text()).toBe('Install')
    await button.trigger('click')

    expect(wrapper.emitted('install')).toEqual([['example-pay', '2.0.0']])
    expect(wrapper.emitted('details')).toBeUndefined()
  })

  it('offers Update for an installed plugin with a newer version', async () => {
    const wrapper = await mountBrowser()
    const button = wrapper.get('[data-testid="registry-install-example-registrar"]')

    expect(button.text()).toBe('Update to 2.0.0')
    await button.trigger('click')

    expect(wrapper.emitted('install')).toEqual([['example-registrar', '2.0.0']])
  })

  it('shows a plain active card, with no install button, for a current plugin', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.find('[data-testid="plugin-card-badge-active-example-host"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-card-version-example-host"]').text()).toContain('2.0.0')
    expect(wrapper.find('[data-testid="registry-install-example-host"]').exists()).toBe(false)
  })

  it('offers the gear only for an installed plugin and emits its id', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.find('[data-testid="registry-settings-example-pay"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="registry-settings-example-registrar"]').exists()).toBe(true)
    await wrapper.get('[data-testid="registry-settings-example-host"]').trigger('click')

    expect(wrapper.emitted('details')).toEqual([['example-host']])
    expect(wrapper.emitted('install')).toBeUndefined()
  })

  it('disables only the card that installs and leaves the others usable', async () => {
    const wrapper = await mountBrowser({ installingIds: ['example-pay'] })

    expect(wrapper.get('[data-testid="registry-install-example-pay"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="registry-install-example-pay"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="registry-install-example-registrar"]').attributes('disabled')).toBeUndefined()
  })

  it('lists the cards in one column, then two, then three as the screen widens', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="plugin-registry-list"]').classes()).toEqual(
      expect.arrayContaining(['grid-cols-1', 'md:grid-cols-2', 'lg:grid-cols-3']),
    )
  })

  it('offers Details instead of Install for a paid plugin without a saved key', async () => {
    const paid = { ...fresh, id: 'example-paid', pricing: PluginPricing.Paid }
    const wrapper = await mountBrowser({ items: [paid] })

    expect(wrapper.find('[data-testid="registry-install-example-paid"]').exists()).toBe(false)
    await wrapper.get('[data-testid="registry-details-example-paid"]').trigger('click')

    expect(wrapper.emitted('details')).toEqual([['example-paid']])
    expect(wrapper.emitted('install')).toBeUndefined()
  })

  it('offers Details for a private plugin without a saved key', async () => {
    const priv = { ...fresh, id: 'example-private', pricing: PluginPricing.Private }
    const wrapper = await mountBrowser({ items: [priv] })

    expect(wrapper.find('[data-testid="registry-install-example-private"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="registry-details-example-private"]').exists()).toBe(true)
  })

  it('follows requiresLicence when the listing carries it', async () => {
    const keyed = { ...fresh, id: 'example-keyed', pricing: PluginPricing.Free, requiresLicence: true }
    const open = { ...fresh, id: 'example-open', pricing: PluginPricing.Paid, requiresLicence: false }
    const wrapper = await mountBrowser({ items: [keyed, open] })

    expect(wrapper.find('[data-testid="registry-details-example-keyed"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="registry-install-example-open"]').exists()).toBe(true)
  })

  it('offers Install for a paid plugin whose key is saved', async () => {
    const paid = { ...fresh, id: 'example-paid', pricing: PluginPricing.Paid }
    const wrapper = await mountBrowser({ items: [paid], licensedIds: ['example-paid'] })

    expect(wrapper.find('[data-testid="registry-install-example-paid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="registry-details-example-paid"]').exists()).toBe(false)
  })

  it('wears the update badge on an installed plugin with a newer version', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="plugin-card-badge-update-example-registrar"]').text()).toContain('2.0.0')
    expect(wrapper.find('[data-testid="plugin-card-badge-update-example-host"]').exists()).toBe(false)
  })

  it('wears the staged badge with the staged version', async () => {
    const wrapper = await mountBrowser({ stagedVersions: { 'example-registrar': '2.0.0' } })

    expect(wrapper.get('[data-testid="plugin-card-badge-staged-example-registrar"]').text()).toContain('2.0.0')
  })

  it('shows the empty state when nothing matches', async () => {
    const wrapper = await mountBrowser({ items: [], total: 0 })

    expect(wrapper.get('[data-testid="plugin-registry-empty"]').text()).toContain('No plugins found')
    expect(wrapper.find('[data-testid="plugin-registry-list"]').exists()).toBe(false)
  })

  it('draws one card placeholder per page slot in the real grid while the first page loads', async () => {
    const wrapper = await mountBrowser({ items: [], total: 0, loading: true, pageSize: 6 })

    expect(wrapper.get('[data-testid="plugin-registry-list"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-testid="plugin-registry-list"]').findAll('[data-testid="plugin-card-skeleton"]')).toHaveLength(6)
    expect(wrapper.find('[data-testid="plugin-registry-total"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-registry-empty"]').exists()).toBe(false)
  })

  it('shows the API sentence and a retry for a failed load', async () => {
    const wrapper = await mountBrowser({ items: [], total: 0, error: 'Registry unavailable.' })

    expect(wrapper.get('[data-testid="plugin-registry-error"]').text()).toContain('Registry unavailable.')
    await wrapper.get('[data-testid="plugin-registry-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('emits the chosen page', async () => {
    const wrapper = await mountBrowser({ total: 30, pageSize: 10 })

    await wrapper.get('[data-testid="plugin-registry-pagination-next"]').trigger('click')

    expect(wrapper.emitted('update:page')).toEqual([[2]])
  })
})
