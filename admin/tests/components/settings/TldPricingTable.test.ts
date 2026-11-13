/**
 * Tests for TldPricingTable: a row per period, live margin, symbols in the headers, and the
 * events that carry an edited price.
 */
import { describe, expect, it } from 'vitest'
import TldPricingTable from '@/components/settings/TldPricingTable.vue'
import { TLD_PERIODS } from '@/lib/tldPricing'
import { mountScreen } from '../../helpers/mount'

/** Props for a table whose first period is priced. */
const props = {
  title: 'Registration pricing',
  testid: 'tld-register',
  costSymbol: '$',
  sellSymbol: '€',
  cost: { '1': 10, '2': 0, '3': 0, '5': 0, '10': 0 },
  sell: { '1': 12.5, '2': 0, '3': 0, '5': 0, '10': 0 },
}

describe('TldPricingTable', () => {
  it('has a titled section, the symbols in the headers and a row per period', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, props)

    expect(wrapper.get('h2').text()).toBe('Registration pricing')
    expect(wrapper.text()).toContain('Cost ($)')
    expect(wrapper.text()).toContain('Sell (€)')
    expect(wrapper.findAll('tbody tr')).toHaveLength(TLD_PERIODS.length)
  })

  it('keeps the title and header and draws a placeholder row per period while loading', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, { ...props, loading: true })

    expect(wrapper.get('h2').text()).toBe('Registration pricing')
    expect(wrapper.text()).toContain('Cost ($)')
    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] tr')).toHaveLength(TLD_PERIODS.length)
    expect(wrapper.find('[data-testid="tld-register-cost-1"]').exists()).toBe(false)
  })

  it('writes the singular for one year and the plural after', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, props)
    const rows = wrapper.findAll('tbody tr')

    expect(rows[0]?.text()).toContain('1 Year')
    expect(rows[1]?.text()).toContain('2 Years')
  })

  it('shows the margin of a priced period and a dash for an unpriced one', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, props)

    expect(wrapper.get('[data-testid="tld-register-margin-1"]').text()).toBe('+25.0%')
    expect(wrapper.get('[data-testid="tld-register-margin-2"]').text()).toBe('—')
  })

  it('tones a loss red', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, { ...props, sell: { ...props.sell, '1': 8 } })

    expect(wrapper.get('[data-testid="tld-register-margin-1"]').classes()).toContain('text-status-red')
  })

  it('emits the period and value when a price is typed', async () => {
    const { wrapper } = await mountScreen(TldPricingTable, props)

    await wrapper.get('[data-testid="tld-register-cost-2"] input').setValue('4.5')

    expect(wrapper.emitted('update:cost')?.[0]).toEqual(['2', 4.5])
  })
})
