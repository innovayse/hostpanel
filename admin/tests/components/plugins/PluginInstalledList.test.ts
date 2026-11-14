/**
 * Unit tests for PluginInstalledList: the card grid, the gear and the Update button, the refusal notice,
 * the placeholders, the empty and error states, and the filters held in the address.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import PluginInstalledList from '@/components/plugins/PluginInstalledList.vue'
import { mountScreen } from '../../helpers/mount'
import type { PluginListItemDto } from '@/types/plugin'

/** One loaded plugin as the list endpoint returns it. */
const plugin = {
  id: 'example-pay',
  name: 'Example Pay',
  version: '1.2.0',
  author: 'Hostpanel',
  description: 'Card payments',
  type: 'payment',
  category: 'Payments',
  color: null,
  iconUrl: null,
  isLoaded: true,
  isBuiltIn: false,
  status: 'Loaded',
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
  capabilities: [],
} as unknown as PluginListItemDto

/**
 * Mounts the list.
 *
 * @param props - Props to change.
 * @param url - The address the screen opens at.
 * @returns The wrapper and the router.
 */
const mountList = async (props: Record<string, unknown> = {}, url = '/plugins') =>
  mountScreen(
    PluginInstalledList,
    { plugins: [plugin], updateVersions: {}, loading: false, error: null, updatingIds: [], ...props },
    url,
    '/plugins',
  )

describe('PluginInstalledList', () => {
  it('draws a card per plugin with a gear that opens its page', async () => {
    const { wrapper, router } = await mountList()

    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').text()).toContain('Example Pay')
    const gear = wrapper.get('[data-testid="plugin-settings-example-pay"]')
    expect(gear.attributes('aria-label')).toBeTruthy()
    await gear.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/plugins/example-pay')
  })

  it('offers Update instead of the gear when a newer version is on offer, and emits it', async () => {
    const { wrapper } = await mountList({ updateVersions: { 'example-pay': '2.0.0' } })

    expect(wrapper.find('[data-testid="plugin-settings-example-pay"]').exists()).toBe(false)
    await wrapper.get('[data-testid="plugin-update-button-example-pay"]').trigger('click')

    expect(wrapper.emitted('update')).toEqual([['example-pay', '2.0.0']])
  })

  it('explains the broken rule of a refused plugin in the card notice', async () => {
    const refused = { ...plugin, isLoaded: false, status: 'Refused', refusedRule: 'LabelKeyMissing', refusedDetail: 'menu.title' }
    const { wrapper } = await mountList({ plugins: [refused], updateVersions: { 'example-pay': '2.0.0' } })

    const notice = wrapper.get('[data-testid="plugin-refusal-example-pay"]')
    expect(notice.text()).toContain('A text key used by the plugin is missing from its English texts.')
    expect(notice.text()).toContain('menu.title')
    expect(wrapper.find('[data-testid="plugin-update-button-example-pay"]').exists()).toBe(false)
  })

  it('draws card placeholders while the first load runs', async () => {
    const { wrapper } = await mountList({ plugins: [], loading: true })

    expect(wrapper.get('[data-testid="plugins-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('[data-testid="plugin-card-skeleton"]')).toHaveLength(6)
  })

  it('shows the empty state when nothing is installed', async () => {
    const { wrapper } = await mountList({ plugins: [] })

    expect(wrapper.find('[data-testid="plugins-empty"]').exists()).toBe(true)
  })

  it('shows the error sentence and emits retry', async () => {
    const { wrapper } = await mountList({ error: 'Plugins are unavailable.' })

    expect(wrapper.get('[data-testid="plugins-error"]').text()).toContain('Plugins are unavailable.')
    await wrapper.get('[data-testid="plugins-error-retry"]').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('narrows the list by the search text in the address and shows the no-match state', async () => {
    const mailer = { ...plugin, id: 'example-mail', name: 'Mailer', description: 'Sends email' }
    const narrowed = await mountList({ plugins: [plugin, mailer] }, '/plugins?installedSearch=mail')
    expect(narrowed.wrapper.find('[data-testid="plugin-card-example-mail"]').exists()).toBe(true)
    expect(narrowed.wrapper.find('[data-testid="plugin-card-example-pay"]').exists()).toBe(false)

    const none = await mountList({}, '/plugins?installedSearch=nothing-like-this')
    expect(none.wrapper.find('[data-testid="plugins-no-match"]').exists()).toBe(true)
  })
})
