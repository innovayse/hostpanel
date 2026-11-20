/**
 * Unit tests for UiSortableRows: a `<tbody>` in the wide table, a box-less `div` of cards on a phone, and the order it reports.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { PropType } from 'vue'
import { mount } from '@vue/test-utils'
import UiSortableRows from '@/components/ui/UiSortableRows.vue'
import { UI_TABLE_CONTEXT, UiTableDensity } from '@/utils/ui/injection'

vi.mock('vuedraggable', async () => {
  const { defineComponent: define, h: render } = await import('vue')
  return {
    default: define({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Row[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        render(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/** A row as the tests use it. */
interface Row {
  /** Row id. */
  id: number
}

/** The rows of every test. */
const rows: Row[] = [{ id: 1 }, { id: 2 }]

/**
 * Mounts the rows inside a table context.
 *
 * @param phone Whether the table is drawn as cards.
 * @returns The mounted rows.
 */
const mountRows = (phone: boolean) => mount(
  defineComponent({
    provide: { [UI_TABLE_CONTEXT as symbol]: { density: UiTableDensity.Plain, phone } },
    render: () => h(UiSortableRows<Row>, { modelValue: rows, itemKey: 'id' }, {
      item: ({ element }: { element: Row }) => h('span', { 'data-testid': 'row' }, String(element.id)),
    }),
  }),
)

describe('UiSortableRows', () => {

  it('is a tbody of the rows in the wide table', () => {
    const wrapper = mountRows(false)

    expect(wrapper.element.tagName).toBe('TBODY')
    expect(wrapper.findAll('[data-testid="row"]')).toHaveLength(2)
  })

  it('is a box-less presentational div of the rows on a phone', () => {
    const wrapper = mountRows(true)

    expect(wrapper.element.tagName).toBe('DIV')
    expect(wrapper.classes()).toContain('contents')
    expect(wrapper.attributes('role')).toBe('presentation')
  })
})
