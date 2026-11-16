/**
 * Unit tests for ComparisonTable: hidden without rows, one column per plan, and a dash for a plan
 * that does not carry a line.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import ComparisonTable from '~/templates/aurora/sections/AuroraComparisonTable.vue'
import { useAuroraGlobals, PLANS } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

describe('ComparisonTable', () => {
  it('renders nothing when no plan has a specification', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(ComparisonTable, { props: { plans: PLANS, rows: [] } })

    expect(wrapper.find('[data-testid="comparison-table"]').exists()).toBe(false)
  })

  it('renders a header per plan and a row per specification line', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(ComparisonTable, {
      props: { plans: PLANS.slice(0, 2), rows: [{ label: 'Disk', values: ['10 GB', '50 GB'] }, { label: 'Sites', values: ['1', '5'] }] },
    })

    expect(wrapper.findAll('th').map((cell) => cell.text())).toEqual(['aurora.comparison.feature', 'Starter', 'Business'])
    expect(wrapper.find('[data-testid="comparison-row-Disk"]').text()).toContain('10 GB')
    expect(wrapper.find('[data-testid="comparison-row-Disk"]').text()).toContain('50 GB')
  })

  it('shows a clear marker where a plan has no such line', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(ComparisonTable, {
      props: { plans: PLANS.slice(0, 2), rows: [{ label: 'Backups', values: ['Daily', ''] }, { label: 'Disk', values: ['10 GB', '50 GB'] }, { label: 'Sites', values: ['1', '5'] }] },
    })

    const cells = wrapper.find('[data-testid="comparison-row-Backups"]').findAll('td')
    expect(cells.map((cell) => cell.text())).toEqual(['Backups', 'Daily', 'aurora.comparison.notIncluded'])
  })

  it('hides the table when the plans share fewer than two lines', async () => {
    await loadGlobals()
    const wrapper = mountWithKit(ComparisonTable, {
      props: { plans: PLANS.slice(0, 2), rows: [{ label: 'A', values: ['x', ''] }, { label: 'B', values: ['', 'y'] }] },
    })

    expect(wrapper.find('[data-testid="comparison-table"]').exists()).toBe(false)
  })
})
