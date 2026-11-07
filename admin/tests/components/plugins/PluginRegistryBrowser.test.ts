/**
 * Tests for PluginRegistryBrowser: a card per listing with its Verified badge and price, Install for a
 * plugin that is absent, Update for one with a newer version, a plain badge for one that is current,
 * the name button that opens details, the Details action for a paid plugin without a key, Uninstall, per card busy state, the
 * staged and update states, the debounced search, the empty, loading and error states, and the page event.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginRegistryBrowser from '@/components/plugins/PluginRegistryBrowser.vue'
import { PluginCategory, PluginPricing } from '@/types/plugin'
import type { PluginRegistryItemDto } from '@/types/plugin'
import { mountScreen } from '../../helpers/mount'
import { pickOption } from '../../helpers/select'

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
    query: '',
    category: '',
    loading: false,
    error: null,
    installingIds: [],
    removingId: null,
    ...overrides,
  })
  return wrapper
}

afterEach(() => {
  vi.useRealTimers()
})

describe('PluginRegistryBrowser', () => {
  it('shows a card per listing with the verified badge only for a verified publisher', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="registry-card-example-pay"]').text()).toContain('Example Pay')
    expect(wrapper.get('[data-testid="registry-card-example-pay"]').text()).toContain('by Hostpanel')
    expect(wrapper.find('[data-testid="registry-verified-example-pay"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="registry-verified-example-registrar"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="registry-card-example-registrar"]').text()).toContain('Paid')
    expect(wrapper.get('[data-testid="registry-card-example-pay"]').text()).toContain('Free')
  })

  it('shows how many plugins match above the list', async () => {
    const wrapper = await mountBrowser({ total: 31 })

    expect(wrapper.get('[data-testid="plugin-registry-total"]').text()).toBe('31 plugins')
  })

  it('labels a private listing', async () => {
    const wrapper = await mountBrowser({ items: [{ ...fresh, pricing: PluginPricing.Private }] })

    expect(wrapper.get('[data-testid="registry-card-example-pay"]').text()).toContain('Private')
  })

  it('offers Install for a plugin that is not installed and emits its id and latest version', async () => {
    const wrapper = await mountBrowser()
    const button = wrapper.get('[data-testid="registry-install-example-pay"]')

    expect(button.text()).toBe('Install')
    await button.trigger('click')

    expect(wrapper.emitted('install')).toEqual([['example-pay', '2.0.0']])
  })

  it('offers Update for an installed plugin with a newer version', async () => {
    const wrapper = await mountBrowser()
    const button = wrapper.get('[data-testid="registry-install-example-registrar"]')

    expect(button.text()).toBe('Update to 2.0.0')
    await button.trigger('click')

    expect(wrapper.emitted('install')).toEqual([['example-registrar', '2.0.0']])
  })

  it('shows a plain installed badge, with no install button, for a current plugin', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="registry-installed-example-host"]').text()).toBe('Installed 2.0.0')
    expect(wrapper.find('[data-testid="registry-install-example-host"]').exists()).toBe(false)
  })

  it('disables only the card that installs and leaves the others usable', async () => {
    const wrapper = await mountBrowser({ installingIds: ['example-pay'] })

    expect(wrapper.get('[data-testid="registry-install-example-pay"]').text()).toBe('Installing…')
    expect(wrapper.get('[data-testid="registry-install-example-pay"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="registry-install-example-registrar"]').attributes('disabled')).toBeUndefined()
  })

  it('opens the details page from the name button', async () => {
    const wrapper = await mountBrowser()

    await wrapper.get('[data-testid="registry-open-example-pay"]').trigger('click')

    expect(wrapper.emitted('details')).toEqual([['example-pay']])
  })

  it('keeps the card itself non interactive, with the name as a real button', async () => {
    const wrapper = await mountBrowser()
    const card = wrapper.get('[data-testid="registry-card-example-pay"]')

    expect(card.attributes('role')).toBeUndefined()
    expect(card.attributes('tabindex')).toBeUndefined()
    await card.trigger('click')
    expect(wrapper.emitted('details')).toBeUndefined()
    expect(wrapper.get('[data-testid="registry-open-example-pay"]').element.tagName).toBe('BUTTON')
  })

  it('does not open the details page when Install is clicked', async () => {
    const wrapper = await mountBrowser()

    await wrapper.get('[data-testid="registry-install-example-pay"]').trigger('click')

    expect(wrapper.emitted('details')).toBeUndefined()
  })

  it('shows the category and the price on a card', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="registry-category-example-pay"]').text()).toBe('Payment')
    expect(wrapper.get('[data-testid="registry-price-example-registrar"]').text()).toBe('Paid')
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

  it('offers Uninstall only for an installed plugin and emits its id', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.find('[data-testid="registry-uninstall-example-pay"]').exists()).toBe(false)
    await wrapper.get('[data-testid="registry-uninstall-example-host"]').trigger('click')

    expect(wrapper.emitted('remove')).toEqual([['example-host']])
    expect(wrapper.emitted('details')).toBeUndefined()
  })

  it('names the card whose removal runs and disables only that one', async () => {
    const wrapper = await mountBrowser({ removingId: 'example-host' })

    expect(wrapper.get('[data-testid="registry-uninstall-example-host"]').text()).toBe('Removing…')
    expect(wrapper.get('[data-testid="registry-uninstall-example-host"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="registry-uninstall-example-registrar"]').attributes('disabled')).toBeUndefined()
  })

  it('shows the update state of an installed plugin with a newer version', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="registry-update-state-example-registrar"]').text()).toContain('2.0.0')
    expect(wrapper.find('[data-testid="registry-update-state-example-host"]').exists()).toBe(false)
  })

  it('shows the staged version instead of the update state', async () => {
    const wrapper = await mountBrowser({ stagedVersions: { 'example-registrar': '2.0.0' } })

    expect(wrapper.get('[data-testid="registry-staged-example-registrar"]').text()).toContain('2.0.0')
    expect(wrapper.find('[data-testid="registry-update-state-example-registrar"]').exists()).toBe(false)
  })

  it('reports the search text once the typing has paused, not per keystroke', async () => {
    // Mount first: the mount helper waits on a real timer, which fake timers would never fire.
    const wrapper = await mountBrowser()
    vi.useFakeTimers()
    const input = wrapper.get('[data-testid="plugin-registry-search"]')

    await input.setValue('pa')
    await input.setValue('pay')
    expect(wrapper.emitted('update:query')).toBeUndefined()
    vi.advanceTimersByTime(300)

    expect(wrapper.emitted('update:query')).toEqual([['pay']])
  })

  it('has a placeholder on the search box and the category select', async () => {
    const wrapper = await mountBrowser()

    expect(wrapper.get('[data-testid="plugin-registry-search"]').attributes('placeholder')).toBe('Search the registry')
    expect(wrapper.get('[data-testid="plugin-registry-category"]').text()).toContain('All categories')
  })

  it('emits the chosen category from the select', async () => {
    const wrapper = await mountBrowser()

    await pickOption(wrapper, 'plugin-registry-category', 'Payment')

    expect(wrapper.emitted('update:category')).toEqual([[PluginCategory.Payment]])
  })

  it('shows the empty state when nothing matches', async () => {
    const wrapper = await mountBrowser({ items: [], total: 0 })

    expect(wrapper.get('[data-testid="plugin-registry-empty"]').text()).toContain('No plugins found')
    expect(wrapper.find('[data-testid="plugin-registry-list"]').exists()).toBe(false)
  })

  it('shows the skeleton while the first page loads', async () => {
    const wrapper = await mountBrowser({ items: [], total: 0, loading: true })

    expect(wrapper.find('[data-testid="plugin-registry-loading"]').exists()).toBe(true)
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
