/**
 * Smoke tests for TestimonialsView: the testimonials table in sort order, status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import TestimonialsView from '@/views/TestimonialsView.vue'
import type { Testimonial } from '@/types/testimonial'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Testimonial[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a testimonial.
 *
 * @param id - Entry id, also its sort order.
 * @param name - Name or text that identifies it.
 * @param extra - Fields to override.
 * @returns The entry.
 */
const entry = (id: number, name: string, extra: Partial<Testimonial> = {}): Testimonial => ({
  id,
  name,
  company: 'Acme',
  avatarUrl: '',
  rating: 4,
  sortOrder: id,
  isActive: true,
  publishedAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { role: 'CTO', quote: `${name} says hello` } },
  ...extra,
})

/** Two entries, the second inactive. */
const entries: Testimonial[] = [entry(1, 'Ann'), entry(2, 'Bob', { isActive: false })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TestimonialsView', () => {
  it('lists the entries in sort order with status badges', async () => {
    stubRouteTable({ 'GET /admin/testimonials': () => json([...entries].reverse()) })
    const { wrapper } = await mountScreen(TestimonialsView)
    const rows = wrapper.findAll('[data-testid^="testimonial-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['testimonial-row-1', 'testimonial-row-2'])
    expect(rows[0]?.text()).toContain('Ann')
    expect(rows[0]?.text()).toContain('CTO, Acme')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('Inactive')
    expect(rows[0]?.text()).toContain('Ann says hello')
    expect(rows[0]?.text()).toContain('4 / 5')
  })

  it('says so when there is nothing yet', async () => {
    stubRouteTable({ 'GET /admin/testimonials': () => json([]) })
    const { wrapper } = await mountScreen(TestimonialsView)

    expect(wrapper.text()).toContain('No testimonials yet')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/testimonials': () => json(entries) })
    const { wrapper, router } = await mountScreen(TestimonialsView)

    await wrapper.get('[data-testid="testimonials-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/testimonials/create')
    })

    await wrapper.get('[data-testid="testimonial-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="testimonial-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/testimonials/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/testimonials': () => json(entries),
      'DELETE /admin/testimonials/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TestimonialsView)

    await wrapper.get('[data-testid="testimonial-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="testimonial-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the testimonial from "Ann"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/testimonials/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/testimonials/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="testimonials-action-error"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/testimonials': () => json(entries),
      'DELETE /admin/testimonials/1': () => problem('The entry is pinned.', 409),
    })
    const { wrapper } = await mountScreen(TestimonialsView)

    await wrapper.get('[data-testid="testimonial-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="testimonial-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="testimonials-action-error"]').text()).toBe('The entry is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/testimonials': () => json(entries),
      'PUT /admin/testimonials/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(TestimonialsView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/testimonials/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })
})
