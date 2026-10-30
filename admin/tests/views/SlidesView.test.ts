/**
 * Smoke tests for SlidesView: the slide table, audience and status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import SlidesView from '@/views/SlidesView.vue'
import type { Slide } from '@/types/slide'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Slide[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a slide with an English title.
 *
 * @param id - Slide id, also its sort order.
 * @param title - English title.
 * @param extra - Fields to override.
 * @returns The slide.
 */
const slide = (id: number, title: string, extra: Partial<Slide> = {}): Slide => ({
  id,
  iconName: 'star',
  brandColor: '#06b6d4',
  imageUrl: '/img/a.png',
  demoUrl: null,
  learnMoreUrl: null,
  productId: null,
  productName: null,
  sortOrder: id,
  isActive: true,
  targetAudience: 'All',
  visibleFrom: null,
  visibleUntil: null,
  createdAt: '2026-01-01T00:00:00Z',
  translations: [{ locale: 'en', title, tagline: 'Tag', description: null, features: null, ctaText: null, ctaUrl: null }],
  ...extra,
})

/** Two slides, the second hidden from guests only and inactive. */
const slides: Slide[] = [slide(1, 'Welcome'), slide(2, 'Members', { isActive: false, targetAudience: 'Authenticated' })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SlidesView', () => {
  it('lists the slides in sort order with audience and status badges', async () => {
    stubRouteTable({ 'GET /admin/slides': () => json([...slides].reverse()) })
    const { wrapper } = await mountScreen(SlidesView)
    const rows = wrapper.findAll('[data-testid^="slide-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['slide-row-1', 'slide-row-2'])
    expect(rows[0]?.text()).toContain('Welcome')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[0]?.text()).toContain('Always')
    expect(rows[1]?.text()).toContain('Inactive')
    expect(rows[1]?.text()).toContain('Authenticated')
  })

  it('writes a visibility window in the viewer locale', async () => {
    stubRouteTable({ 'GET /admin/slides': () => json([slide(1, 'Dated', { visibleFrom: '2026-03-01T00:00:00Z' })]) })
    const { wrapper } = await mountScreen(SlidesView)

    expect(wrapper.get('[data-testid="slide-row-1"]').text()).toContain('From')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/slides': () => json(slides) })
    const { wrapper, router } = await mountScreen(SlidesView)

    await wrapper.get('[data-testid="slides-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/slides/create')
    })

    await wrapper.get('[data-testid="slide-row-2"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="slide-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/settings/slides/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/slides': () => json(slides),
      'DELETE /admin/slides/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(SlidesView)

    await wrapper.get('[data-testid="slide-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="slide-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete "Welcome"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/slides/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/slides/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="slides-action-error"]').text()).toBe('Record deleted.')
  })

  it('does not delete when the confirmation is dismissed', async () => {
    const mock = stubRouteTable({ 'GET /admin/slides': () => json(slides) })
    const { wrapper } = await mountScreen(SlidesView)

    await wrapper.get('[data-testid="slide-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="slide-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')

    expect(requestsOf(mock)).not.toContain('DELETE /admin/slides/1')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
  })

  it('loads again from the retry of a refused load', async () => {
    let calls = 0
    stubRouteTable({
      'GET /admin/slides': () => {
        calls += 1
        return calls === 1 ? problem('Not allowed.', 403) : json(slides)
      },
    })
    const { wrapper } = await mountScreen(SlidesView)

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="slides-error"]').text()).toBe('Not allowed.')
    })
    await wrapper.get('[data-testid="slides-error-retry"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.find('[data-testid="slide-row-1"]').exists()).toBe(true)
    })
    await wrapper.get('[data-testid="slide-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    expect(wrapper.find('[data-testid="slide-delete-1"]').exists()).toBe(true)
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/slides': () => json(slides),
      'DELETE /admin/slides/1': () => problem('The slide is pinned.', 409),
    })
    const { wrapper } = await mountScreen(SlidesView)

    await wrapper.get('[data-testid="slide-row-1"]').get('[data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="slide-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="slides-action-error"]').text()).toBe('The slide is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/slides': () => json(slides),
      'PUT /admin/slides/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(SlidesView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/slides/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })

  it('says so when there are no slides', async () => {
    stubRouteTable({ 'GET /admin/slides': () => json([]) })
    const { wrapper } = await mountScreen(SlidesView)

    expect(wrapper.text()).toContain('No slides found')
  })
})
