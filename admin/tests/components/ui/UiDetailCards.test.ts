/**
 * Unit tests for UiDetailCards: schema cards of rows or a table, the real body in a slot and the loading skeleton.
 */
import { describe, expect, it } from 'vitest'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import { mountWithApp } from '../../helpers/mount'
import type { SkeletonDetailCard } from '@/utils/ui/skeletonSchema'

/** The first column: a card of rows. */
const profile: SkeletonDetailCard = { key: 'profile', title: 'shared.clients', rows: ['shared.name', 'shared.email'] }

/** The second column: a card with a table and a pager. */
const history: SkeletonDetailCard = {
  key: 'history',
  title: 'shared.status',
  table: { columns: [{ key: 'id', label: 'shared.id' }, { key: 'status', label: 'shared.status' }], pagination: true },
}

/**
 * Mounts the cards with the real catalogue.
 *
 * @param props - Props of the cards.
 * @param slots - Slots of the cards, as markup.
 * @returns The mounted wrapper.
 */
const mountCards = (props: Record<string, unknown>, slots: Record<string, string> = {}) =>
  mountWithApp(UiDetailCards, { props, slots })

describe('UiDetailCards', () => {
  it('draws a card per schema card with its title, and the real body from its slot', () => {
    const wrapper = mountCards({ columns: [[profile], [history]] }, { 'card-profile': '<p data-testid="body">Real</p>' })

    expect(wrapper.findAll('[data-testid^="ui-detail-card-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="ui-detail-card-profile"] h2').text()).toBe('Clients')
    expect(wrapper.get('[data-testid="ui-detail-card-profile"] [data-testid="body"]').text()).toBe('Real')
  })

  it('puts the columns side by side, and one column in a one column grid', () => {
    const two = mountCards({ columns: [[profile], [history]] })
    const one = mountCards({ columns: [[profile, history]] })

    expect(two.get('.grid').classes()).toContain('md:grid-cols-2')
    expect(one.get('.grid').classes()).toContain('grid-cols-1')
    expect(one.get('.grid').classes()).not.toContain('md:grid-cols-2')
  })

  it('stacks the cards of one column', () => {
    const wrapper = mountCards({ columns: [[profile, history]] })

    expect(wrapper.findAll('[data-testid^="ui-detail-card-"]')).toHaveLength(2)
  })

  describe('while loading', () => {
    it('keeps the titles and the row captions and draws a value bar under each', () => {
      const wrapper = mountCards({ columns: [[profile]], loading: true }, { 'card-profile': '<p data-testid="body">Real</p>' })

      expect(wrapper.get('h2').text()).toBe('Clients')
      expect(wrapper.text()).toContain('Name')
      expect(wrapper.text()).toContain('Email')
      expect(wrapper.findAll('[data-testid="ui-detail-row"]')).toHaveLength(2)
      expect(wrapper.find('[data-testid="body"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="ui-detail-card-profile"]').attributes('aria-busy')).toBe('true')
    })

    it('draws the real header captions and placeholder rows of a table card, and its pager', () => {
      const wrapper = mountCards({ columns: [[history]], loading: true })

      expect(wrapper.findAll('[role="columnheader"]').map((header) => header.text())).toEqual(['ID', 'Status'])
      expect(wrapper.find('[data-testid="ui-table-skeleton"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="pagination-range"]').exists()).toBe(true)
    })
  })

  describe('the layout', () => {
    it('shares two columns by the ratio of the schema', () => {
      const wrapper = mountCards({ columns: [[profile], [history]], ratio: 'three-quarters' })

      expect(wrapper.get('div').classes().join(' ')).toContain('3fr')
    })

    it('flows a single column of half cards two across, a card without a span taking the whole row', () => {
      const wrapper = mountCards({
        columns: [[{ key: 'top' }, { key: 'left', span: 'half' }, { key: 'right', span: 'half' }]],
        ratio: 'two-thirds',
      })

      expect(wrapper.get('div').classes()).toContain('items-start')
      expect(wrapper.get('[data-testid="ui-detail-card-top"]').classes()).toContain('col-span-full')
      expect(wrapper.get('[data-testid="ui-detail-card-left"]').classes()).not.toContain('col-span-full')
    })

    it('keeps one column a plain stack when no card is half wide', () => {
      const wrapper = mountCards({ columns: [[profile, history]] })

      expect(wrapper.get('div').classes()).not.toContain('items-start')
    })
  })

  describe('the kinds of body of a loading card', () => {
    it('draws a head, field placeholders, lines, boxes and buttons from the counts of the schema', () => {
      const wrapper = mountCards({
        columns: [[{ key: 'config', identity: true, fields: 4, lines: 2, tiles: 3, actions: 2 }]],
        loading: true,
      })

      expect(wrapper.find('[data-testid="ui-detail-identity"]').exists()).toBe(true)
      expect(wrapper.findAll('[data-testid="ui-field-skeleton"]')).toHaveLength(4)
      expect(wrapper.get('[data-testid="ui-detail-tiles"]').element.children).toHaveLength(3)
      expect(wrapper.get('[data-testid="ui-form-actions"]').element.children).toHaveLength(2)
    })

    it('draws a card with tabs as a bare strip of the real captions, not clickable, and the slot when loaded', () => {
      const columns = [[{ key: 'strip', tabs: [{ key: 'one', label: 'shared.name' }, { key: 'two', label: 'shared.email' }] }]]
      const loading = mountCards({ columns, loading: true })
      const loaded = mountCards({ columns }, { 'card-strip': '<p data-testid="real">Real</p>' })

      expect(loading.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['Name', 'Email'])
      expect(loading.get('[role="tablist"]').element.closest('[inert]')).not.toBeNull()
      expect(loaded.find('[role="tab"]').exists()).toBe(false)
      expect(loaded.find('[data-testid="real"]').exists()).toBe(true)
    })

    it('draws only the cards named by loadingKeys as skeletons while the others show their slots', () => {
      const wrapper = mountCards({ columns: [[profile, history]], loadingKeys: ['history'] }, { 'card-profile': '<p data-testid="body">Real</p>', 'card-history': '<p data-testid="late">Late</p>' })

      expect(wrapper.find('[data-testid="body"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="late"]').exists()).toBe(false)
      expect(wrapper.get('[data-testid="ui-detail-card-history"]').attributes('aria-busy')).toBe('true')
      expect(wrapper.get('[data-testid="ui-detail-card-profile"]').attributes('aria-busy')).toBeUndefined()
    })
  })
})
