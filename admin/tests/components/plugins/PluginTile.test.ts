/**
 * Tests for PluginTile: the title row with the name and the Verified mark, the one meta line, the
 * two-line description, the badges and tags in one row with the "+N" fold, the footer with downloads
 * and version on the left and the actions slot on the right, the name that opens the page only when
 * the plugin is openable, and the default slot.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginTile from '@/components/plugins/PluginTile.vue'
import { PluginTileBadgeKey } from '@/types/pluginTile'
import { UiTone } from '@/utils/ui/tone'
import { createTestI18n } from '../../helpers/i18n'

/** Props of a plain tile. */
const base = {
  id: 'example-pay',
  name: 'Example Pay',
  meta: ['Hostpanel', 'Payment', 'Free'],
  description: 'Card payments',
}

/**
 * Mounts the tile.
 *
 * @param props - Props to add or change.
 * @param slots - Slots to fill.
 * @returns The wrapper.
 */
const mountTile = (props: Record<string, unknown> = {}, slots: Record<string, string> = {}) =>
  mount(PluginTile, { props: { ...base, ...props }, slots, global: { plugins: [createTestI18n()] } })

describe('PluginTile', () => {
  it('shows the name and joins the meta parts into one line', () => {
    const wrapper = mountTile()

    expect(wrapper.text()).toContain('Example Pay')
    expect(wrapper.get('[data-testid="plugin-tile-meta-example-pay"]').text()).toBe('Hostpanel · Payment · Free')
  })

  it('titles the card with the card heading look, not a link colour', () => {
    const heading = mountTile().get('h3')

    expect(heading.classes()).toContain('text-text-primary')
    expect(heading.classes()).not.toContain('text-primary-400')
  })

  it('clamps the description to two lines', () => {
    expect(mountTile().get('[data-testid="plugin-tile-description-example-pay"]').classes()).toContain('line-clamp-2')
  })

  it('shows the Verified mark next to the name only for a verified plugin', () => {
    expect(mountTile({ verified: true }).get('[data-testid="plugin-tile-verified-example-pay"]').text()).toBe('Verified')
    expect(mountTile().find('[data-testid="plugin-tile-verified-example-pay"]').exists()).toBe(false)
  })

  it('makes the name a button that opens the page when openable and emits the id', async () => {
    const wrapper = mountTile({ openable: true })
    const open = wrapper.get('[data-testid="plugin-tile-open-example-pay"]')

    expect(open.element.tagName).toBe('BUTTON')
    expect(open.classes()).toContain('after:absolute')
    await open.trigger('click')

    expect(wrapper.emitted('open')).toEqual([['example-pay']])
  })

  it('draws a plain name, with no button, when the plugin has no page', () => {
    expect(mountTile().find('[data-testid="plugin-tile-open-example-pay"]').exists()).toBe(false)
  })

  it('shows hover and focus states on the card and no glow', () => {
    const classes = mountTile({ openable: true }).classes()

    expect(classes).toContain('hover:border-accent')
    expect(classes).toContain('has-[:focus-visible]:border-accent')
    expect(classes.some((c) => /(^|:)shadow-(lg|xl|2xl|primary)/.test(c))).toBe(false)
  })

  it('stretches to the row with the footer pinned to the bottom', () => {
    const wrapper = mountTile()

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-full', 'flex', 'flex-col']))
    expect(wrapper.get('[data-testid="plugin-tile-footer-example-pay"]').classes()).toContain('mt-auto')
  })

  it('shows the rating only when it is given', () => {
    const rated = mountTile({ rating: { average: 4.5, count: 12 } })

    expect(rated.get('[data-testid="plugin-tile-rating-example-pay"]').text()).toContain('4.5 (12)')
    expect(mountTile().find('[data-testid="plugin-tile-rating-example-pay"]').exists()).toBe(false)
  })

  it('draws the badges before the tags in one row, each with its own test id', () => {
    const wrapper = mountTile({
      badges: [
        { key: PluginTileBadgeKey.Status, label: 'Active', tone: UiTone.Success },
        { key: 'capability-endpoints', label: 'Serves pages', tone: UiTone.Neutral },
      ],
      tags: ['cards'],
    })
    const chips = wrapper.get('[data-testid="plugin-tile-chips-example-pay"]')

    expect(chips.get('[data-testid="plugin-tile-badge-example-pay-status"]').text()).toBe('Active')
    expect(chips.get('[data-testid="plugin-tile-badge-example-pay-capability-endpoints"]').text()).toBe('Serves pages')
    expect(chips.text().indexOf('Active')).toBeLessThan(chips.text().indexOf('#cards'))
  })

  it('shows the first three tags and folds the rest into +N', () => {
    const wrapper = mountTile({ tags: ['cards', 'billing', 'eu', 'sepa', 'wallets'] })

    expect(wrapper.find('[data-testid="plugin-tile-tag-example-pay-eu"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-tile-tag-example-pay-sepa"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-tile-more-tags-example-pay"]').text()).toBe('+2')
  })

  it('draws no chip row without badges or tags, and no +N when every tag shows', () => {
    expect(mountTile().find('[data-testid="plugin-tile-chips-example-pay"]').exists()).toBe(false)
    expect(mountTile({ tags: ['a', 'b', 'c'] }).find('[data-testid="plugin-tile-more-tags-example-pay"]').exists()).toBe(false)
  })

  it('shows downloads and version on the left of the footer, each with an icon', () => {
    const wrapper = mountTile({ downloads: 1234, version: '2.0.0' })

    expect(wrapper.get('[data-testid="plugin-tile-downloads-example-pay"]').text()).toBe('1,234 downloads')
    expect(wrapper.get('[data-testid="plugin-tile-downloads-example-pay"]').find('svg').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-tile-version-example-pay"]').text()).toBe('v2.0.0')
  })

  it('leaves out the downloads and the version when they are not given', () => {
    const wrapper = mountTile()

    expect(wrapper.find('[data-testid="plugin-tile-downloads-example-pay"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-tile-version-example-pay"]').exists()).toBe(false)
  })

  it('renders the actions slot in the footer and the default slot above the chips', () => {
    const wrapper = mountTile({}, { actions: '<button data-testid="act">Go</button>', default: '<p data-testid="extra">Extra</p>' })

    expect(wrapper.get('[data-testid="plugin-tile-footer-example-pay"]').find('[data-testid="act"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-tile-footer-example-pay"] [data-testid="extra"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="extra"]').text()).toBe('Extra')
  })

  it('shows the plugin icon when it has one and the colour block with the initial when not', () => {
    const withIcon = mountTile({ iconUrl: '/api/admin/plugins/example-pay/icon' })
    const without = mountTile()

    expect(withIcon.get('[data-testid="plugin-avatar-image"]').attributes('src')).toBe('/api/admin/plugins/example-pay/icon')
    expect(without.get('[data-testid="plugin-avatar-fallback"] text').text()).toBe('E')
  })
})
