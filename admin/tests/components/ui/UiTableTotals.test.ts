/**
 * Unit tests for UiTableTotals: the totals of a line item table are the rows of its foot, the caption spans the columns
 * before the amount column and the figure sits in the amount column, so the figures line up with the amounts above.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import type { PropType } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { useRowSelection } from '@/composables/useRowSelection'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTableTotals from '@/components/ui/UiTableTotals.vue'
import type { UiTotalRow } from '@/components/ui/UiTableTotals.vue'
import { createTestI18n } from '../../helpers/i18n'

/** The lines every test draws. */
const ROWS: UiTotalRow[] = [
  { key: 'subtotal', label: 'Sub total', value: '$10.00', testid: 'sub' },
  { key: 'total', label: 'Total due', value: '$12.00', strong: true, testid: 'due' },
]

/**
 * A table of three columns (description, quantity, amount) with one line and the totals under it.
 *
 * @returns A component to mount.
 */
const harness = () => defineComponent({
  props: { rows: { type: Array as PropType<UiTotalRow[]>, default: () => ROWS } },
  render() {
    return h(UiTable, null, {
      default: () => [
        h(UiTableHead, null, { default: () => h(UiTableRow, null, {
          default: () => [
            h(UiTableHeader, null, { default: () => 'Description' }),
            h(UiTableHeader, null, { default: () => 'Qty' }),
            h(UiTableHeader, { numeric: true }, { default: () => 'Amount' }),
          ],
        }) }),
        h(UiTableBody, null, { default: () => h(UiTableRow, null, {
          default: () => [
            h(UiTableCell, null, { default: () => 'Setup' }),
            h(UiTableCell, null, { default: () => '1' }),
            h(UiTableCell, { numeric: true }, { default: () => '$10.00' }),
          ],
        }) }),
        h(UiTableTotals, { rows: this.rows }),
      ],
    })
  },
})

describe('UiTableTotals', () => {
  it('draws every line as a row of the table foot', () => {
    const wrapper = mount(harness(), { global: { plugins: [createTestI18n()] } })

    expect(wrapper.find('tfoot').exists()).toBe(true)
    expect(wrapper.findAll('tfoot tr')).toHaveLength(2)
  })

  it('spans the caption over the columns before the amount and puts the figure in the amount column', () => {
    const wrapper = mount(harness(), { global: { plugins: [createTestI18n()] } })
    const cells = wrapper.findAll('tfoot tr:first-child td')

    expect(cells).toHaveLength(2)
    expect(cells[0]?.attributes('colspan')).toBe('2')
    expect(cells[0]?.text()).toBe('Sub total')
    expect(cells[1]?.text()).toBe('$10.00')
    expect(cells[1]?.attributes('data-testid')).toBe('sub')
  })

  it('right aligns both cells and stresses only the closing line', () => {
    const wrapper = mount(harness(), { global: { plugins: [createTestI18n()] } })
    const first = wrapper.findAll('tfoot tr:first-child td')
    const last = wrapper.findAll('tfoot tr:last-child td')

    expect(first[1]?.classes()).toContain('text-end')
    expect(first[1]?.classes()).toContain('text-text-muted')
    expect(last[1]?.classes()).toContain('font-semibold')
    expect(last[1]?.classes()).not.toContain('text-text-muted')
  })

  it('derives the span from the real columns: a select column and a trailing actions column included', async () => {
    const wrapper = mount(defineComponent({
      setup() {
        const selection = useRowSelection(() => [1])
        return () => h(UiTable, { selection }, {
          default: () => [
            h(UiTableHead, null, { default: () => h(UiTableRow, null, {
              default: () => [
                h(UiTableHeader, null, { default: () => 'Description' }),
                h(UiTableHeader, null, { default: () => 'Qty' }),
                h(UiTableHeader, null, { default: () => 'Price' }),
                h(UiTableHeader, { numeric: true }, { default: () => 'Amount' }),
                h(UiTableHeader, { actions: true }),
              ],
            }) }),
            h(UiTableTotals, { rows: ROWS }),
          ],
        })
      },
    }), { global: { plugins: [createTestI18n()] } })
    await flushPromises()

    expect(wrapper.find('tfoot tr td').attributes('colspan')).toBe('4')
  })
})

describe('UiTableTotals on a phone', () => {
  /** The `matchMedia` of the test document before a test replaced it. */
  const originalMatchMedia = window.matchMedia

  afterEach(() => {
    Object.defineProperty(window, 'matchMedia', { configurable: true, writable: true, value: originalMatchMedia })
  })

  it('draws each line as the caption at the start and the figure at the end, with no column caption', async () => {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: (query: string) => ({ matches: true, media: query, addEventListener: () => undefined, removeEventListener: () => undefined }),
    })
    const wrapper = mount(harness(), { global: { plugins: [createTestI18n()] } })
    await flushPromises()

    const line = wrapper.findAll('div.justify-between').find((node) => node.text().includes('Total due'))
    expect(line?.findAll('span').map((node) => node.text())).toEqual(['Total due', '$12.00'])
    expect(line?.classes()).toContain('font-semibold')
    expect(wrapper.find('tfoot').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Amount: $')
  })
})
