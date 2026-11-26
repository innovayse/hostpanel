/**
 * Unit tests for UiTable: native table, status row for loading and empty, shared density.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableFoot from '@/components/ui/UiTableFoot.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { UiColumnKind, UiColumnSize, UiHideBelow } from '@/utils/ui/table'
import { createTestI18n } from '../../helpers/i18n'

/** Global options giving the table its i18n plugin. */
const global = () => ({ plugins: [createTestI18n()] })

/**
 * A loading table whose header has one column per given shape.
 *
 * @param columns Props of each header cell.
 * @returns A component to mount.
 */
const headerHarness = (columns: Array<Record<string, unknown>>) => defineComponent({
  render: () => h(UiTable, { loading: true, skeletonRows: 2 }, {
    default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, {
      default: () => columns.map((props) => h(UiTableHeader, props, { default: () => 'H' })),
    }) }),
  }),
})

describe('UiTable', () => {
  it('names the header of an actions column "Actions" in the active language, with no caption from the screen', () => {
    const wrapper = mount(defineComponent({
      render: () => h(UiTable, null, {
        default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, {
          default: () => [h(UiTableHeader, null, { default: () => 'Name' }), h(UiTableHeader, { actions: true })],
        }) }),
      }),
    }), { global: global() })

    expect(wrapper.findAll('th').map((th) => th.text())).toEqual(['Name', 'Actions'])
  })

  it('renders the native table with its slot', () => {
    const wrapper = mount(UiTable, { slots: { default: '<tbody><tr><td>content</td></tr></tbody>' }, global: global() })

    expect(wrapper.find('table').exists()).toBe(true)
    expect(wrapper.text()).toBe('content')
  })

  it('scrolls sideways inside its own box at every width, so a wide table never leaves its card', () => {
    const wrapper = mount(UiTable, { global: global() })

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['overflow-x-auto', 'mb-1.5']))
    expect(wrapper.classes()).not.toContain('overflow-y-auto')
    expect(wrapper.element.firstElementChild?.tagName).toBe('TABLE')
  })

  it('fades the edge of the box that still hides columns, and drops the fade once the end is reached', async () => {
    const wrapper = mount(UiTable, { global: global(), attachTo: document.body })
    const box = wrapper.element as HTMLElement
    // jsdom has no layout: the box is given a width narrower than its content.
    Object.defineProperty(box, 'clientWidth', { configurable: true, value: 300 })
    Object.defineProperty(box, 'scrollWidth', { configurable: true, value: 900 })

    box.dispatchEvent(new Event('scroll'))
    await nextTick()
    expect(wrapper.classes().join(' ')).toContain('mask-image')

    Object.defineProperty(box, 'scrollLeft', { configurable: true, value: 600 })
    box.dispatchEvent(new Event('scroll'))
    await nextTick()
    expect(wrapper.classes().join(' ')).toContain('to_left')
    wrapper.unmount()
  })

  it('can drop its scrolling box, so a dropdown opened from a cell is not clipped', () => {
    const wrapper = mount(UiTable, { props: { scrollable: false }, global: global() })

    expect(wrapper.classes()).not.toContain('overflow-x-auto')
    expect(wrapper.find('table').exists()).toBe(true)
  })

  it('without its own box fills the sticky header, which sticks to the page scroll container', () => {
    const wrapper = mount(UiTable, { props: { scrollable: false }, global: global() })

    expect(wrapper.get('table').classes()).toContain('[&_thead]:bg-(--glass-strong-bg)')
  })

  it('scrolls vertically under a height cap, so its sticky header has a box to stick to', () => {
    const wrapper = mount(UiTable, { props: { maxHeight: 'md' }, global: global() })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['overflow-x-auto', 'overflow-y-auto', 'max-h-[28rem]']))
  })

  it('drops the height cap together with the scrolling box', () => {
    const wrapper = mount(UiTable, { props: { scrollable: false, maxHeight: 'md' }, global: global() })

    expect(wrapper.classes()).toEqual([])
  })

  it('draws the placeholder cells at the size of their column', async () => {
    const wrapper = mount(headerHarness([{ size: UiColumnSize.Compact }, { size: UiColumnSize.Grow }, {}]), { global: global() })
    await nextTick()

    const cells = wrapper.findAll('[data-testid="ui-table-skeleton"] tr:first-child td')
    expect(cells[0]?.classes()).toEqual(expect.arrayContaining(['w-px', 'whitespace-nowrap']))
    expect(cells[1]?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
    expect(cells[2]?.classes()).toEqual(expect.arrayContaining(['w-[28%]', 'max-w-0', 'truncate']))
  })

  it('spreads the width over several data columns without a warning', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const wrapper = mount(headerHarness([{ size: UiColumnSize.Grow }, {}, { kind: UiColumnKind.Date }]), { global: global() })
    await nextTick()

    const cells = wrapper.findAll('[data-testid="ui-table-skeleton"] tr:first-child td')
    expect(cells[0]?.classes()).toContain('w-[28%]')
    expect(cells[1]?.classes()).toContain('w-[28%]')
    expect(cells[2]?.classes()).toContain('w-[14%]')
    expect(warn).not.toHaveBeenCalled()
    warn.mockRestore()
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiTable, { attrs: { class: 'x-class', 'data-testid': 'probe' }, global: global() })

    const table = wrapper.get('table')
    expect(table.classes()).toContain('x-class')
    expect(table.attributes('data-testid')).toBe('probe')
  })

  it('shows the empty message in a row as wide as the header', async () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { empty: true }, {
        default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'A' }),
          h(UiTableHeader, null, { default: () => 'B' }),
          h(UiTableHeader, null, { default: () => 'C' }),
          h(UiTableHeader, null, { default: () => 'D' }),
        ] }) }),
      }),
    })

    const wrapper = mount(Harness, { global: global() })
    await nextTick()

    expect(wrapper.text()).toContain('No data')
    expect(wrapper.get('tbody td').attributes('colspan')).toBe('4')
  })

  it('lets a screen replace the empty message', () => {
    const wrapper = mount(UiTable, {
      props: { empty: true },
      slots: { empty: 'No clients yet' },
      global: global(),
    })

    expect(wrapper.text()).toBe('No clients yet')
  })

  it('draws the default message as a compact empty state with an icon, and the screen text when given', () => {
    const plain = mount(UiTable, { props: { empty: true }, global: global() })
    const custom = mount(UiTable, { props: { empty: true, emptyText: 'No claims' }, global: global() })

    expect(plain.get('[data-testid="ui-table-empty"]').text()).toBe('No data')
    expect(plain.find('[data-testid="ui-empty-state-icon"]').exists()).toBe(true)
    expect(custom.get('[data-testid="ui-table-empty"]').text()).toBe('No claims')
  })

  it('leaves a custom empty slot to its own spacing', () => {
    const custom = mount(UiTable, { props: { empty: true }, slots: { empty: 'Nothing' }, global: global() })

    expect(custom.find('[data-testid="ui-table-empty"]').exists()).toBe(false)
    expect(custom.get('td').classes()).not.toContain('py-8')
  })

  it('hides the column captions of the empty table but keeps the row as wide as the header', async () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { empty: true }, {
        default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'A' }),
          h(UiTableHeader, null, { default: () => 'B' }),
        ] }) }),
      }),
    })

    const wrapper = mount(Harness, { global: global(), attachTo: document.body })
    await nextTick()

    expect(wrapper.get('thead').isVisible()).toBe(false)
    expect(wrapper.get('tbody td').attributes('colspan')).toBe('2')
    wrapper.unmount()
  })

  it('keeps the real header and draws one bar per column in every placeholder row while loading', async () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { loading: true, empty: true, skeletonRows: 4 }, {
        default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'Name' }),
          h(UiTableHeader, null, { default: () => 'Email' }),
          h(UiTableHeader, null, { default: () => 'Role' }),
        ] }) }),
      }),
    })

    const wrapper = mount(Harness, { global: global() })
    await nextTick()

    expect(wrapper.get('th').text()).toBe('Name')
    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] tr')).toHaveLength(4)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(12)
    expect(wrapper.text()).not.toContain('No data')
  })

  it('renders neither its body nor its foot while loading', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { loading: true }, {
        default: () => [
          h(UiTableBody, null, { default: () => h('tr', null, [h('td', 'real row')]) }),
          h(UiTableFoot, null, { default: () => h('tr', null, [h('td', 'total')]) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.text()).not.toContain('real row')
    expect(wrapper.find('tfoot').exists()).toBe(false)
  })

  it('renders its body and foot once loading ends', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, null, {
        default: () => [
          h(UiTableBody, null, { default: () => h('tr', null, [h('td', 'real row')]) }),
          h(UiTableFoot, null, { default: () => h('tr', null, [h('td', 'total')]) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.text()).toContain('real row')
    expect(wrapper.find('tfoot').exists()).toBe(true)
  })

  it('gives placeholder cells the height and padding of real cells', async () => {
    const wrapper = mount(headerHarness([{}]), { global: global() })
    await nextTick()

    expect(wrapper.get('tbody td').classes()).toEqual(expect.arrayContaining(['h-12', 'px-3', 'py-2']))
  })

  it('draws placeholder cells with the visibility and width classes of their header column', async () => {
    const wrapper = mount(headerHarness([{}, { hideBelow: UiHideBelow.Md }, { actions: true }]), { global: global() })
    await nextTick()

    const cells = wrapper.findAll('[data-testid="ui-table-skeleton"] tr:first-child td')
    expect(cells).toHaveLength(3)
    expect(cells[0]?.classes()).not.toContain('hidden')
    expect(cells[1]?.classes()).toEqual(expect.arrayContaining(['hidden', 'md:table-cell']))
    expect(cells[2]?.classes()).toEqual(expect.arrayContaining(['w-px', 'text-end']))
  })

  it('drops the placeholder cell of a header that unmounts', async () => {
    /** Whether the second header is shown. */
    const shown = ref(true)
    /** Table whose second header unmounts when the flag is cleared. */
    const Harness = defineComponent({
      render: () => h(UiTable, { loading: true, skeletonRows: 1 }, {
        default: () => h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => [
          h(UiTableHeader, null, { default: () => 'A' }),
          shown.value ? h(UiTableHeader, null, { default: () => 'B' }) : null,
        ] }) }),
      }),
    })
    const wrapper = mount(Harness, { global: global() })
    await nextTick()
    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] td')).toHaveLength(2)

    shown.value = false
    await nextTick()

    expect(wrapper.findAll('[data-testid="ui-table-skeleton"] td')).toHaveLength(1)
  })

  it('draws no status row when there is data', () => {
    expect(mount(UiTable, { global: global() }).find('tbody').exists()).toBe(false)
  })

  it('gives header and data cells the same padding and alignment by default', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, null, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => 'A' }) }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => '1' }) }) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.get('th').classes()).toEqual(expect.arrayContaining(['px-3', 'h-10', 'text-start']))
    expect(wrapper.get('td').classes()).toEqual(expect.arrayContaining(['px-3', 'h-12', 'text-start']))
  })

  it('hands its density to the header and data cells', () => {
    const Harness = defineComponent({
      render: () => h(UiTable, { density: 'compact' }, {
        default: () => [
          h(UiTableHead, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableHeader, null, { default: () => 'A' }) }) }),
          h(UiTableBody, null, { default: () => h(UiTableRow, null, { default: () => h(UiTableCell, null, { default: () => '1' }) }) }),
        ],
      }),
    })

    const wrapper = mount(Harness, { global: global() })

    expect(wrapper.get('th').classes()).toContain('px-3')
    expect(wrapper.get('td').classes()).toEqual(expect.arrayContaining(['px-3', 'h-10']))
  })
})
