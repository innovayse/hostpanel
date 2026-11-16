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
 * @returns The wrapper.
 */
const mountCard = (overrides: Partial<PluginCardModel> = {}, slots: Record<string, string> = {}) =>
  mount(PluginCard, {
    props: { plugin: { ...model, ...overrides }, to: { name: 'plugin-details', params: { id: 'example-pay' } } },
    slots,
    global: { plugins: [createTestI18n(), router] },
  })

describe('PluginCard', () => {
  it('shows the name, the meta line, the description, the tags and the footer', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-card-link-example-pay"]').text()).toBe('Example Pay')
    expect(wrapper.get('[data-testid="plugin-card-meta-example-pay"]').text()).toBe('by Hostpanel · Payment · Free')
    expect(wrapper.get('[data-testid="plugin-card-description-example-pay"]').classes()).toContain('line-clamp-2')
    expect(wrapper.get('[data-testid="plugin-card-tags-example-pay"]').text()).toContain('#cards')
    expect(wrapper.get('[data-testid="plugin-card-downloads-example-pay"]').text()).toContain('1,234')
    expect(wrapper.get('[data-testid="plugin-card-version-example-pay"]').text()).toBe('v1.2.0')
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
        PluginCardBadge.PendingRestart,
        PluginCardBadge.Refused,
        PluginCardBadge.Staged,
        PluginCardBadge.Update,
        PluginCardBadge.BuiltIn,
      ],
      stagedVersion: '1.3.0',
      updateVersion: '2.0.0',
    })

    expect(wrapper.get('[data-testid="plugin-card-badge-active-example-pay"]').text()).toBe('Active')
    expect(wrapper.get('[data-testid="plugin-card-badge-pendingRestart-example-pay"]').text()).toBe('Pending restart')
    expect(wrapper.get('[data-testid="plugin-card-badge-refused-example-pay"]').text()).toBe('Refused')
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
})
