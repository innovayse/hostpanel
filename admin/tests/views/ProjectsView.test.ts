/**
 * Smoke tests for ProjectsView: the projects table in sort order, status badges, navigation to the
 * create and edit pages, the delete confirmation, and the new order after a drag. The drag library
 * is replaced by a stub that renders the item slot for each element.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { PropType } from 'vue'
import ProjectsView from '@/views/ProjectsView.vue'
import type { Project } from '@/types/project'
import { json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

vi.mock('vuedraggable', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    default: defineComponent({
      name: 'DraggableStub',
      props: {
        modelValue: { type: Array as PropType<Project[]>, default: () => [] },
        tag: { type: String, default: 'div' },
      },
      emits: ['update:modelValue', 'end'],
      setup: (props, { slots }) => () =>
        h(props.tag, props.modelValue.map((element, index) => slots.item?.({ element, index }))),
    }),
  }
})

/**
 * Builds a project.
 *
 * @param id - Entry id, also its sort order.
 * @param name - Name or text that identifies it.
 * @param extra - Fields to override.
 * @returns The entry.
 */
const entry = (id: number, name: string, extra: Partial<Project> = {}): Project => ({
  id,
  slug: name.toLowerCase(),
  imageUrls: [],
  url: '',
  sortOrder: id,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  translations: { en: { title: name, summary: `About ${name}`, description: '' } },
  ...extra,
})

/** Two entries, the second inactive. */
const entries: Project[] = [entry(1, 'Site'), entry(2, 'Shop', { isActive: false })]

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ProjectsView', () => {
  it('lists the entries in sort order with status badges', async () => {
    stubRouteTable({ 'GET /admin/projects': () => json([...entries].reverse()) })
    const { wrapper } = await mountScreen(ProjectsView)
    const rows = wrapper.findAll('[data-testid^="project-row-"]')

    expect(rows.map((row) => row.attributes('data-testid'))).toEqual(['project-row-1', 'project-row-2'])
    expect(rows[0]?.text()).toContain('Site')
    expect(rows[0]?.text()).toContain('About Site')
    expect(rows[0]?.text()).toContain('Active')
    expect(rows[1]?.text()).toContain('Inactive')
    expect(rows[0]?.text()).toContain('site')
  })

  it('says so when there is nothing yet', async () => {
    stubRouteTable({ 'GET /admin/projects': () => json([]) })
    const { wrapper } = await mountScreen(ProjectsView)

    expect(wrapper.text()).toContain('No projects yet')
  })

  it('navigates to the create and edit pages', async () => {
    stubRouteTable({ 'GET /admin/projects': () => json(entries) })
    const { wrapper, router } = await mountScreen(ProjectsView)

    await wrapper.get('[data-testid="projects-add"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/projects/create')
    })

    await wrapper.get('[data-testid="project-row-2"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="project-edit-2"]').trigger('click')
    await vi.waitFor(() => {
      expect(router.currentRoute.value.path).toBe('/storefront/projects/2/edit')
    })
  })

  it('asks before deleting, then deletes and reloads', async () => {
    const mock = stubRouteTable({
      'GET /admin/projects': () => json(entries),
      'DELETE /admin/projects/1': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(ProjectsView)

    await wrapper.get('[data-testid="project-row-1"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="project-delete-1"]').trigger('click')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Delete the project "Site"?')
    expect(requestsOf(mock)).not.toContain('DELETE /admin/projects/1')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('DELETE /admin/projects/1')
    })
    await vi.waitFor(() => {
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false)
    })
    expect(wrapper.get('[data-testid="projects-action-error"]').text()).toBe('Record deleted.')
  })

  it('shows the API sentence when a delete is refused', async () => {
    stubRouteTable({
      'GET /admin/projects': () => json(entries),
      'DELETE /admin/projects/1': () => problem('The entry is pinned.', 409),
    })
    const { wrapper } = await mountScreen(ProjectsView)

    await wrapper.get('[data-testid="project-row-1"] [data-testid="row-actions-trigger"]').trigger('click')
    await wrapper.get('[data-testid="project-delete-1"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')

    await vi.waitFor(() => {
      expect(wrapper.get('[data-testid="projects-action-error"]').text()).toBe('The entry is pinned.')
    })
  })

  it('saves the new order after a drag ends', async () => {
    const mock = stubRouteTable({
      'GET /admin/projects': () => json(entries),
      'PUT /admin/projects/order': () => new Response(null, { status: 204 }),
    })
    const { wrapper } = await mountScreen(ProjectsView)

    wrapper.findComponent({ name: 'DraggableStub' }).vm.$emit('end')
    await vi.waitFor(() => {
      expect(requestsOf(mock)).toContain('PUT /admin/projects/order')
    })

    const put = mock.mock.calls.find((call) => String(call[0]).endsWith('/order'))
    expect(JSON.parse((put?.[1] as RequestInit | undefined)?.body as string)).toEqual({
      items: [{ id: 1, sortOrder: 0 }, { id: 2, sortOrder: 1 }],
    })
  })
})
