/**
 * Unit tests for PluginCard: the title row, the meta line, the clamped description, the tags, the footer, the badges, the
 * stretched details link and the action and notice slots.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import PluginCard from '@/components/plugins/PluginCard.vue'
import { PluginCardBadge } from '@/types/plugin'
import type { PluginCardModel } from '@/types/plugin'
import { createTestI18n } from '../../helpers/i18n'

/** A running plugin with everything filled in. */
const model: PluginCardModel = {
  id: 'example-pay',
  name: 'Example Pay',
  iconUrl: null,
  color: '#123456',
  verified: true,
  publisher: 'Hostpanel',
  category: 'Payment',
  pricingLabel: 'Free',
  description: 'Card payments',
  tags: ['cards', 'sepa'],
  downloads: 1234,
  version: '1.2.0',
  badges: [PluginCardBadge.Active],
  updateVersion: null,
  stagedVersion: null,
  refusal: null,
  failedChange: null,
}

/** Router that knows the details route, so the link resolves. */
const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/', component: { template: '<div />' } }, { path: '/plugins/:id', name: 'plugin-details', component: { template: '<div />' } }],
})

/**
 * Mounts the card.
 *
 * @param overrides - Model fields to change.
 * @param slots - Slot content to pass.
 * @param checkingUpdate - Whether the registry is still asked for a newer version.
 * @returns The wrapper.
 */
const mountCard = (overrides: Partial<PluginCardModel> = {}, slots: Record<string, string> = {}, checkingUpdate = false) =>
  mount(PluginCard, {
    props: { plugin: { ...model, ...overrides }, checkingUpdate, to: { name: 'plugin-details', params: { id: 'example-pay' } } },
    slots,
    global: { plugins: [createTestI18n(), router] },
  })

describe('PluginCard', () => {
  it('names the plugin icon after the plugin so the list is readable without the text beside it', () => {
    const wrapper = mountCard({ iconUrl: '/api/admin/plugins/example-pay/icon' })

    expect(wrapper.get('[data-testid="plugin-avatar-image"]').attributes('alt')).toBe('Example Pay')
  })

  it('shows the name, the meta line, the description, the tags and the footer', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-card-link-example-pay"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-card-meta-example-pay"]').text()).toBe('by Hostpanel · Payment · Free')
    expect(wrapper.get('[data-testid="plugin-card-description-example-pay"]').classes()).toContain('line-clamp-2')
    expect(wrapper.get('[data-testid="plugin-card-tags-example-pay"]').text()).toContain('#cards')
    expect(wrapper.get('[data-testid="plugin-card-downloads-example-pay"]').text()).toContain('1.2K')
    expect(wrapper.get('[data-testid="plugin-card-version-example-pay"]').text()).toBe('v1.2.0')
  })

  it('draws a placeholder where the update badge will appear while the update is checked', () => {
    expect(mountCard().find('[data-testid="plugin-card-badge-checking-example-pay"]').exists()).toBe(false)
    expect(mountCard({}, {}, true).find('[data-testid="plugin-card-badge-checking-example-pay"]').exists()).toBe(true)
  })

  it('lets the name run over two lines and keeps the badges out of the title row', () => {
    const wrapper = mountCard({ name: 'Connector for a very long plugin name', badges: [PluginCardBadge.Active, PluginCardBadge.Update], updateVersion: '2.0.0' })
    const title = wrapper.get('[data-testid="plugin-card-title-example-pay"]')

    expect(title.get('.text-text-primary').classes()).toContain('line-clamp-2')
    expect(title.get('.text-text-primary').classes()).not.toContain('truncate')
    expect(title.find('[data-testid="plugin-card-badge-active-example-pay"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-card-badges-example-pay"]').findAll('[data-testid^="plugin-card-badge-"]')).toHaveLength(2)
  })

  it('starts the name next to the logo instead of centering it in the title row', () => {
    const link = mountCard().get('[data-testid="plugin-card-link-example-pay"]')

    expect(link.classes()).toContain('flex-1')
    expect(link.classes()).toContain('justify-start')
    expect(link.classes()).not.toContain('justify-center')
  })

  it('draws no badge row when there is neither a badge nor a placeholder', () => {
    expect(mountCard({ badges: [] }).find('[data-testid="plugin-card-badges-example-pay"]').exists()).toBe(false)
    expect(mountCard({ badges: [] }, {}, true).find('[data-testid="plugin-card-badges-example-pay"]').exists()).toBe(true)
  })

  it('keeps the downloads and version on one line, with the count in the short form', () => {
    const wrapper = mountCard({ downloads: 27 })
    const downloads = wrapper.get('[data-testid="plugin-card-downloads-example-pay"]')

    expect(downloads.text()).toBe('27 downloads')
    expect(downloads.find('.whitespace-nowrap').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-card-version-example-pay"]').classes()).toContain('whitespace-nowrap')
  })

  it('draws the tags as one chip row that never wraps', () => {
    const wrapper = mountCard({ tags: ['control-panel', 'cyberpanel', 'hosting', 'openlitespeed', 'provisioning'] })
    const chips = wrapper.get('[data-testid="plugin-card-tags-example-pay"]')

    expect(chips.classes()).toContain('min-w-0')
    expect(chips.findAll('[data-testid="ui-chip-row-chip"]').every(chip => chip.classes().includes('whitespace-nowrap'))).toBe(true)
    expect(chips.findAll('[data-testid="ui-chip-row-chip"]')[0]?.text()).toBe('#control-panel')
  })

  it('fills the height of its grid row and pins the footer to the bottom', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-card-example-pay"]').classes()).toEqual(expect.arrayContaining(['h-full', 'flex', 'flex-col']))
    expect(wrapper.get('[data-testid="plugin-card-footer-example-pay"]').classes()).toContain('mt-auto')
  })

  it('gives the footer a fixed height so the card does not jump', () => {
    expect(mountCard().get('[data-testid="plugin-card-footer-example-pay"]').classes()).toContain('h-12')
  })

  it('shows the Failed badge with the reason and the version still running', () => {
    const wrapper = mountCard({ badges: [PluginCardBadge.Active, PluginCardBadge.ChangeFailed], failedChange: { error: 'Missing dependency.', restoredVersion: '1.0.0' } })

    expect(wrapper.get('[data-testid="plugin-card-badge-changeFailed-example-pay"]').text()).toBe('Update failed')
    expect(wrapper.get('[data-testid="plugin-card-failure-example-pay"]').text()).toBe('Update failed: Missing dependency. Version 1.0.0 is still running.')
  })

  it('shows no failure line when the last change did not fail', () => {
    expect(mountCard().find('[data-testid="plugin-card-failure-example-pay"]').exists()).toBe(false)
  })

  it('stretches the name link over the card and gives the name the primary tone', () => {
    const wrapper = mountCard()
    const link = wrapper.get('[data-testid="plugin-card-link-example-pay"]')

    expect(link.attributes('href')).toBe('/plugins/example-pay')
    expect(link.classes()).toContain('after:absolute')
    expect(link.find('.text-text-primary').exists()).toBe(true)
  })

  it('marks a verified publisher', () => {
    expect(mountCard().find('[data-testid="plugin-card-verified-example-pay"]').exists()).toBe(true)
    expect(mountCard({ verified: false }).find('[data-testid="plugin-card-verified-example-pay"]').exists()).toBe(false)
  })

  it('leaves out the parts it has no data for', () => {
    const wrapper = mountCard({ publisher: '', pricingLabel: '', tags: [], downloads: null })

    expect(wrapper.get('[data-testid="plugin-card-meta-example-pay"]').text()).toBe('Payment')
    expect(wrapper.find('[data-testid="plugin-card-tags-example-pay"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-card-downloads-example-pay"]').exists()).toBe(false)
  })

  it('labels every badge, naming the versions where it has one', () => {
    const wrapper = mountCard({
      badges: [
        PluginCardBadge.Active,
        PluginCardBadge.Disabled,
        PluginCardBadge.PendingRestart,
        PluginCardBadge.Refused,
        PluginCardBadge.LoadFailed,
        PluginCardBadge.Staged,
        PluginCardBadge.Update,
        PluginCardBadge.BuiltIn,
      ],
      stagedVersion: '1.3.0',
      updateVersion: '2.0.0',
    })

    expect(wrapper.get('[data-testid="plugin-card-badge-active-example-pay"]').text()).toBe('Active')
    expect(wrapper.get('[data-testid="plugin-card-badge-disabled-example-pay"]').text()).toBe('Disabled')
    expect(wrapper.get('[data-testid="plugin-card-badge-pendingRestart-example-pay"]').text()).toBe('Pending restart')
    expect(wrapper.get('[data-testid="plugin-card-badge-refused-example-pay"]').text()).toBe('Refused')
    expect(wrapper.get('[data-testid="plugin-card-badge-loadFailed-example-pay"]').text()).toBe('Failed to load')
    expect(wrapper.get('[data-testid="plugin-card-badge-staged-example-pay"]').text()).toContain('1.3.0')
    expect(wrapper.get('[data-testid="plugin-card-badge-update-example-pay"]').text()).toContain('2.0.0')
    expect(wrapper.get('[data-testid="plugin-card-badge-builtIn-example-pay"]').text()).toBe('Built-in')
  })

  it('renders the action and notice slots, and omits them when empty', () => {
    const withSlots = mountCard({}, { action: '<button data-testid="act">Remove</button>', notice: '<p data-testid="note">Broken</p>' })

    expect(withSlots.get('[data-testid="act"]').text()).toBe('Remove')
    expect(withSlots.get('[data-testid="note"]').text()).toBe('Broken')
    expect(mountCard().find('[data-testid="act"]').exists()).toBe(false)
  })

  it('keeps a long name beside the logo and a long staged badge on its own row, cut short with the full text as a tooltip', () => {
    const name = 'Domain registrar connector with an unusually long plugin name'
    const wrapper = mountCard({ name, badges: [PluginCardBadge.Active, PluginCardBadge.Staged], stagedVersion: '1.4.3' })
    const title = wrapper.get('[data-testid="plugin-card-title-example-pay"]')
    const link = wrapper.get('[data-testid="plugin-card-link-example-pay"]')
    const staged = wrapper.get('[data-testid="plugin-card-badge-staged-example-pay"]')

    expect(link.text()).toBe(name)
    expect(link.classes()).toEqual(expect.arrayContaining(['min-w-0', 'flex-1']))
    expect(link.find('.line-clamp-2').exists()).toBe(true)
    expect(title.find('[data-testid="plugin-card-badge-staged-example-pay"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-card-badges-example-pay"]').classes()).toContain('flex-wrap')
    expect(staged.attributes('title')).toBe('Version 1.4.3 not applied yet')
    expect(staged.classes()).toContain('max-w-full')
    expect(staged.find('.truncate').exists()).toBe(true)
  })

  it('keeps a long tag list inside the card: the chips that do not fit are counted in the "+N" chip', () => {
    const wrapper = mountCard({ tags: ['control-panel', 'authorize-net', 'hosted-payment-page', 'openlitespeed'] })
    const tags = wrapper.get('[data-testid="plugin-card-tags-example-pay"]')

    expect(tags.classes()).toContain('min-w-0')
    expect(tags.findAll('[data-testid="ui-chip-row-chip"]')[0]?.text()).toBe('#control-panel')
  })

  it('keeps the footer figures on one line each and lets them give way before they could overlap the action', () => {
    const wrapper = mountCard({ downloads: 51, version: '1.0.1' }, { action: '<button data-testid="act">Update to 1.0.4</button>' })
    const figures = wrapper.get('[data-testid="plugin-card-version-example-pay"]').element.parentElement

    expect(figures?.classList.contains('overflow-hidden')).toBe(true)
    expect(wrapper.get('[data-testid="plugin-card-downloads-example-pay"]').classes()).toContain('shrink-0')
    expect(wrapper.get('[data-testid="plugin-card-downloads-example-pay"]').find('.whitespace-nowrap').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-card-version-example-pay"]').classes()).toEqual(expect.arrayContaining(['shrink-0', 'whitespace-nowrap']))
    expect(wrapper.get('[data-testid="act"]').element.parentElement?.classList.contains('shrink-0')).toBe(true)
  })
})
