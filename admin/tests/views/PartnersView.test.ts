/**
 * Smoke tests for PartnersView: the partners table in sort order, status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import PartnersView from '@/views/PartnersView.vue'
import type { Partner } from '@/types/partner'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Partner[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a partner.
 *
 * @param id - Entry id, also its sort order.
 * @param name - Name or text that identifies it.
 * @param extra - Fields to override.
 * @returns The entry.
 */
const entry = (id: number, name: string, extra: Partial<Partner> = {}): Partner => ({
  id,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { name } },
  logoUrl: '',
  url: `https://${name.toLowerCase()}.example.com`,
  sortOrder: id,
  isActive: true,
  ...extra,
})

/** Two entries, the second inactive. */
const entries: Partner[] = [entry(1, 'Acme'), entry(2, 'Globex', { isActive: false })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('PartnersView', () => {
  it('lists the entries in sort order with status badges', async () => {
    stubRouteTable({ 'GET /admin/partners': () => json([...entries].reverse()) })
    const { wrapper } = await mountScreen(PartnersView)
    const rows = wrapper.findAll('[data-testid^="partner-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['partner-row-1', 'partner-row-2'])
    expect(rows[0]?.text()).toContain('Acme')
    expect(rows[0]?.text()).toContain('https://acme.example.com')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('Inactive')
    expect(rows[0]?.text()).toContain('https://acme.example.com')
  })

  it('says so when there is nothing yet', async () => {
    stubRouteTable({ 'GET /admin/partners': () => json([]) })
    const { wrapper } = await mountScreen(PartnersView)

    expect(wrapper.text()).toContain('No partners yet')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/partners': () => json(entries) })
    const { wrapper, router } = await mountScreen(PartnersView)

    await wrapper.get('[data-testid="partners-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/partners/create')
    })

    await wrapper.get('[data-testid="partner-row-2"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="partner-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/partners/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/partners': () => json(entries),
      'DELETE /admin/partners/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(PartnersView)

    await wrapper.get('[data-testid="partner-row-1"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="partner-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the partner "Acme"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/partners/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/partners/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="partners-action-error"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/partners': () => json(entries),
      'DELETE /admin/partners/1': () => problem('The entry is pinned.', 409),
    })
    const { wrapper } = await mountScreen(PartnersView)

    await wrapper.get('[data-testid="partner-row-1"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="partner-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="partners-action-error"]').text()).toBe('The entry is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/partners': () => json(entries),
      'PUT /admin/partners/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(PartnersView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/partners/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })
})
