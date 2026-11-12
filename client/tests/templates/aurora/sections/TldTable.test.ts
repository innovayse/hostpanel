/**
 * Unit tests for TldTable: every row without a filter, the category filter, and the empty state.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import TldTable from '~/templates/aurora/sections/TldTable.vue'
import { ALL_CATEGORY } from '~/templates/aurora/types/allcategory'
import type { TldPriceRow } from '~/types/tldpricerow'
import { stubAuroraGlobals } from '../auroraGlobals'

/**
 * Builds one price row.
 *
 * @param tld The extension.
 * @param categories The registrar's category tags.
 * @returns A row with plausible prices.
 */
const row = (tld: string, categories: string[]): TldPriceRow => ({
  tld,
  register: '$10',
  renew: '$12',
  transfer: '$9',
  categories,
  registerAmount: 10,
  transferAmount: 10,
  sellCurrency: 'USD',
})

/** Three extensions in two categories. */
const ROWS = [row('.com', ['Popular']), row('.am', ['Country']), row('.net', ['Popular'])]

describe('TldTable', () => {
  it('lists every extension for the all category, with its first category tag', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(TldTable, { props: { rows: ROWS, active: ALL_CATEGORY } })

    expect(wrapper.findAll('[data-testid^="tld-row-"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="tld-row-.am"]').text()).toContain('Country')
  })

  it('narrows the rows to the selected category', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(TldTable, { props: { rows: ROWS, active: 'Popular' } })

    expect(wrapper.findAll('[data-testid^="tld-row-"]').map((tr) => tr.attributes('data-testid'))).toEqual(['tld-row-.com', 'tld-row-.net'])
  })

  it('shows the empty line when nothing matches', () => {
    stubAuroraGlobals()
    const wrapper = mountWithKit(TldTable, { props: { rows: ROWS, active: 'Unknown' } })

    expect(wrapper.find('[data-testid="tld-empty"]').text()).toBe('aurora.tldTable.empty')
    expect(wrapper.find('[data-testid="tld-empty"]').attributes('colspan')).toBe('5')
  })
})
