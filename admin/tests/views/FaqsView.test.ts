/**
 * Smoke tests for FaqsView: the FAQ entries table in sort order, status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import FaqsView from '@/views/FaqsView.vue'
import type { Faq } from '@/types/faq'
import { FaqCategory } from '@/types/storefrontenums'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Faq[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a faq.
 *
 * @param id - Entry id, also its sort order.
 * @param name - Name or text that identifies it.
 * @param extra - Fields to override.
 * @returns The entry.
 */
const entry = (id: number, name: string, extra: Partial<Faq> = {}): Faq => ({
  id,
  category: FaqCategory.General,
  sortOrder: id,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { question: name, answer: 'Yes.' } },
  ...extra,
})

/** Two entries, the second inactive. */
const entries: Faq[] = [entry(1, 'Why'), entry(2, 'How', { isActive: false, category: FaqCategory.Products })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FaqsView', () => {
  it('lists the entries in sort order with status badges', async () => {
    stubRouteTable({ 'GET /admin/faqs': () => json([...entries].reverse()) })
    const { wrapper } = await mountScreen(FaqsView)
    const rows = wrapper.findAll('[data-testid^="faq-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['faq-row-1', 'faq-row-2'])
    expect(rows[0]?.text()).toContain('Why')
    expect(rows[0]?.text()).toContain('General')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('Inactive')
    expect(rows[0]?.text()).toContain('General')
    expect(rows[1]?.text()).toContain('Products')
  })

  it('says so when there is nothing yet', async () => {
    stubRouteTable({ 'GET /admin/faqs': () => json([]) })
    const { wrapper } = await mountScreen(FaqsView)

    expect(wrapper.text()).toContain('No questions yet')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/faqs': () => json(entries) })
    const { wrapper, router } = await mountScreen(FaqsView)

    await wrapper.get('[data-testid="faqs-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/faqs/create')
    })

    await wrapper.get('[data-testid="faq-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="faq-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/faqs/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/faqs': () => json(entries),
      'DELETE /admin/faqs/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(FaqsView)

    await wrapper.get('[data-testid="faq-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="faq-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the question "Why"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/faqs/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/faqs/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/faqs': () => json(entries),
      'DELETE /admin/faqs/1': () => problem('The entry is pinned.', 409),
    })
    const { wrapper } = await mountScreen(FaqsView)

    await wrapper.get('[data-testid="faq-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="faq-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="faqs-action-error"]').text()).toBe('The entry is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/faqs': () => json(entries),
      'PUT /admin/faqs/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(FaqsView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/faqs/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })
})
