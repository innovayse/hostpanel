/**
 * Smoke tests for LocationsView: the locations table in sort order, status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import LocationsView from '@/views/LocationsView.vue'
import type { ServerLocation } from '@/types/serverlocation'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<ServerLocation[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a location.
 *
 * @param id - Entry id, also its sort order.
 * @param name - Name or text that identifies it.
 * @param extra - Fields to override.
 * @returns The entry.
 */
const entry = (id: number, name: string, extra: Partial<ServerLocation> = {}): ServerLocation => ({
  id,
  countryCode: 'US',
  translations: { en: { name, city: `${name} City`, latencyNote: null } },
  sortOrder: id,
  isActive: true,
  ...extra,
})

/** Two entries, the second inactive. */
const entries: ServerLocation[] = [entry(1, 'Acme'), entry(2, 'Globex', { isActive: false })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('LocationsView', () => {
  it('lists the entries in sort order with status badges', async () => {
    stubRouteTable({ 'GET /admin/locations': () => json([...entries].reverse()) })
    const { wrapper } = await mountScreen(LocationsView)
    const rows = wrapper.findAll('[data-testid^="location-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['location-row-1', 'location-row-2'])
    expect(rows[0]?.text()).toContain('Acme')
    expect(rows[0]?.text()).toContain('US')
    expect(rows[0]?.text()).toContain('Acme City')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('Inactive')
  })

  it('says so when there is nothing yet', async () => {
    stubRouteTable({ 'GET /admin/locations': () => json([]) })
    const { wrapper } = await mountScreen(LocationsView)

    expect(wrapper.text()).toContain('No locations yet')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/locations': () => json(entries) })
    const { wrapper, router } = await mountScreen(LocationsView)

    await wrapper.get('[data-testid="locations-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/locations/create')
    })

    await wrapper.get('[data-testid="location-actions-2"]').trigger('click')
    await wrapper.get('[data-testid="location-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/locations/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/locations': () => json(entries),
      'DELETE /admin/locations/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(LocationsView)

    await wrapper.get('[data-testid="location-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="location-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the location "Acme"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/locations/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/locations/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/locations': () => json(entries),
      'DELETE /admin/locations/1': () => problem('The entry is pinned.', 409),
    })
    const { wrapper } = await mountScreen(LocationsView)

    await wrapper.get('[data-testid="location-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="location-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="locations-action-error"]').text()).toBe('The entry is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/locations': () => json(entries),
      'PUT /admin/locations/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(LocationsView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/locations/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })
})
