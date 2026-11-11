/**
 * Tests for StorefrontListTable: rows sorted by sort order through the row slot, the page states,
 * the Add and edit links, the delete event, the delete question and the order reported after a drag. The
 * drag library is replaced by a stub that renders the item slot for each element.
 */
import { describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import type { PropType } from 'vue'
import type { VueWrapper } from '@vue/test-utils'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import { StorefrontListOutcome } from '@/types/storefront'
import { mountScreen } from '../../helpers/mount'

vi.mock('vuedraggable', async () => {
  const { defineComponent: define, h: render } = await import('vue')
  return {
    default: define({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Widget[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        render(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/** An entry as the tests use it. */
interface Widget {
  /** Entry ID. */
  id: number
  /** Display order. */
  sortOrder: number
  /** Name shown in the row. */
  name: string
}

/** Two entries, the second sorted first. */
const widgets: Widget[] = [
  { id: 1, sortOrder: 1, name: 'Second' },
  { id: 2, sortOrder: 0, name: 'First' },
]

/** Props every mount starts from. */
const baseProps = {
  title: 'Widgets',
  subtitle: 'All widgets',
  addLabel: 'Add widget',
  addTo: '/storefront/faqs/create',
  editTo: (item: { id: number }): string => `/storefront/faqs/${item.id}/edit`,
  loadingLabel: 'Loading widgets...',
  emptyLabel: 'No widgets.',
  deleteTitle: 'Delete widget',
  editLabel: 'Edit widget',
  deleteLabel: 'Remove widget',
  moveUpLabel: 'Move widget up',
  moveDownLabel: 'Move widget down',
  testids: {
    view: 'widgets-view',
    add: 'widgets-add',
    error: 'widgets-error',
    actionError: 'widgets-action-error',
    savingOrder: 'widgets-saving-order',
    row: 'widget-row',
    actions: 'widget-actions',
    edit: 'widget-edit',
    delete: 'widget-delete',
    moveUp: 'widget-move-up',
    moveDown: 'widget-move-down',
  },
  columns: ['Name'],
  items: widgets,
  loading: false,
  error: null,
  actionError: null,
  savingOrder: false,
  deleteTarget: null,
  deleting: false,
  deleteMessage: 'Delete First?',
}

/**
 * Opens the actions menu of one row.
 *
 * @param wrapper - The mounted screen.
 * @param id - Id of the entry whose row menu to open.
 */
const openMenu = async (wrapper: VueWrapper, id: number): Promise<void> => {
  await wrapper.get(`[data-testid="widget-actions-${id}"]`).trigger('click')
}

/**
 * Builds a host that renders the table with a one-cell row slot.
 *
 * @param overrides - Props to change.
 * @returns The host component.
 */
const hostWith = (overrides: Record<string, unknown> = {}) =>
  defineComponent({
    setup: () => () =>
      h(StorefrontListTable, { ...baseProps, ...overrides }, { row: ({ item }: { item: Widget }) => h('td', { 'data-testid': `cell-${item.id}` }, item.name) }),
  })

describe('StorefrontListTable', () => {
  it('lists the entries in sort order with the page header', async () => {
    const { wrapper } = await mountScreen(hostWith())

    expect(wrapper.get('[data-testid="widgets-view"]').text()).toContain('Widgets')
    expect(wrapper.findAll('[data-testid^="widget-row-"]').map((row) => row.attributes('data-testid'))).toEqual(['widget-row-2', 'widget-row-1'])
    expect(wrapper.get('[data-testid="cell-2"]').text()).toBe('First')
  })

  it('says so when there are no entries', async () => {
    const { wrapper } = await mountScreen(hostWith({ items: [] }))

    expect(wrapper.text()).toContain('No widgets.')
  })

  it('shows the loading caption and the load error instead of the table', async () => {
    const loading = await mountScreen(hostWith({ loading: true }))
    expect(loading.wrapper.text()).toContain('Loading widgets...')
    expect(loading.wrapper.find('[data-testid^="widget-row-"]').exists()).toBe(false)

    const failed = await mountScreen(hostWith({ error: 'Could not load.' }))
    expect(failed.wrapper.get('[data-testid="widgets-error"]').text()).toBe('Could not load.')
  })

  it('shows the action error and the saving-order notice', async () => {
    const { wrapper } = await mountScreen(hostWith({ actionError: 'Refused.', savingOrder: true }))

    expect(wrapper.get('[data-testid="widgets-action-error"]').text()).toBe('Refused.')
    expect(wrapper.get('[data-testid="widgets-action-error"]').attributes('data-tone')).toBe('danger')
    expect(wrapper.get('[data-testid="widgets-saving-order"]').text()).toBe('Saving order...')
  })

  it('confirms a finished delete or reorder in the outcome line', async () => {
    const deleted = await mountScreen(hostWith({ outcome: StorefrontListOutcome.Deleted }))
    expect(deleted.wrapper.get('[data-testid="widgets-action-error"]').text()).toBe('Record deleted.')
    expect(deleted.wrapper.get('[data-testid="widgets-action-error"]').attributes('data-tone')).toBe('success')

    const ordered = await mountScreen(hostWith({ outcome: StorefrontListOutcome.OrderSaved }))
    expect(ordered.wrapper.get('[data-testid="widgets-action-error"]').text()).toBe('Order saved.')
  })

  it('asks the page to load again from the retry of a refused load', async () => {
    const { wrapper } = await mountScreen(hostWith({ error: 'Could not load.' }))

    await wrapper.get('[data-testid="widgets-error-retry"]').trigger('click')

    expect(wrapper.findComponent(StorefrontListTable).emitted('retry')).toHaveLength(1)
  })

  it('reports a dismissed delete question', async () => {
    const { wrapper } = await mountScreen(hostWith({ deleteTarget: widgets[1] }))

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(wrapper.findComponent(StorefrontListTable).emitted('cancelDelete')).toHaveLength(1)
    expect(wrapper.findComponent(StorefrontListTable).emitted('confirmDelete')).toBeUndefined()
  })

  it('links Add and edit to their pages and emits stage-delete from the menu', async () => {
    const host = hostWith()
    const { wrapper } = await mountScreen(host)
    const table = wrapper.findComponent(StorefrontListTable)

    expect(wrapper.get('[data-testid="widgets-add"]').attributes('href')).toBe('/storefront/faqs/create')
    await openMenu(wrapper, 1)
    expect(wrapper.get('[data-testid="widget-edit-1"]').attributes('href')).toBe('/storefront/faqs/1/edit')
    await wrapper.get('[data-testid="widget-delete-1"]').trigger('click')

    expect(table.emitted('stageDelete')?.[0]).toEqual([widgets[0]])
  })

  it('draws the delete question for the staged entry and reports the answer', async () => {
    const { wrapper } = await mountScreen(hostWith({ deleteTarget: widgets[1] }))
    const table = wrapper.findComponent(StorefrontListTable)

    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete First?')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(table.emitted('confirmDelete')).toHaveLength(1)
  })

  it('moves a row with the move up and move down menu items and reports the new order', async () => {
    const { wrapper } = await mountScreen(hostWith())
    await openMenu(wrapper, 2)
    expect(wrapper.get('[data-testid="widget-move-up-2"]').attributes('disabled')).toBeDefined()
    await openMenu(wrapper, 1)
    expect(wrapper.get('[data-testid="widget-move-down-1"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="widget-move-down-2"]').trigger('click')

    expect(wrapper.findComponent(StorefrontListTable).emitted('reorder')?.[0]).toEqual([
      [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    ])
  })

  it('labels the empty drag-handle header for screen readers', async () => {
    const { wrapper } = await mountScreen(hostWith())

    expect(wrapper.get('th .sr-only').text()).toBe('Reorder')
  })

  it('reports the new order after a drag ends', async () => {
    const { wrapper } = await mountScreen(hostWith())

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')

    expect(wrapper.findComponent(StorefrontListTable).emitted('reorder')?.[0]).toEqual([
      [{ id: 2, sortOrder: 0 }, { id: 1, sortOrder: 1 }],
    ])
  })
})
