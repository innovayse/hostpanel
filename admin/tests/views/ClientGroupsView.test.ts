/**
 * Mount tests for ClientGroupsView: the table lists the groups, a refused load offers a retry,
 * creating saves through the dialog, and deleting asks first.
 */
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ClientGroupFormDialog from '@/components/clients/ClientGroupFormDialog.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import ClientGroupsView from '@/views/ClientGroupsView.vue'
import { createTestI18n } from '../helpers/i18n'
import { callsOf, json, problem, stubFetch } from '../helpers/fetch'

/** One group as the list endpoint answers it. */
const GROUP = {
  id: 1,
  name: 'VIP',
  description: null,
  discountPercent: 10,
  color: '#112233',
  isDefault: true,
  clientCount: 2,
}

/**
 * Mounts the view against scripted answers and waits for the first load.
 *
 * @param responses - Answers the stubbed API gives in order.
 * @returns The wrapper and the fetch mock.
 */
const mountView = async (...responses: Response[]) => {
  const mock = stubFetch(...responses)
  const wrapper = mount(ClientGroupsView, {
    global: { plugins: [createTestI18n(), createPinia()], stubs: { teleport: true } },
  })
  await flushPromises()
  return { wrapper, mock }
}

/**
 * Finds the delete confirmation among the dialogs on the page (the group form mounts one too).
 *
 * @param wrapper - The mounted view.
 * @returns The confirm-variant dialog.
 */
const confirmDialog = (wrapper: Awaited<ReturnType<typeof mountView>>['wrapper']) => {
  const found = wrapper.findAllComponents(UiDialog).find(dialog => dialog.props('variant') === 'confirm')
  if (!found) {
    throw new Error('The delete confirmation is not open.')
  }
  return found
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientGroupsView', () => {
  it('lists the groups with their discount, count and default badge', async () => {
    const { wrapper } = await mountView(json([GROUP]))

    const row = wrapper.get('[data-testid="client-group-row"]')
    expect(row.text()).toContain('VIP')
    expect(row.text()).toContain('10%')
    expect(row.text()).toContain('Default')
  })

  it('shows the empty state when there are no groups', async () => {
    const { wrapper } = await mountView(json([]))

    expect(wrapper.text()).toContain('No client groups yet.')
  })

  it('shows the refusal with a retry that loads again', async () => {
    const { wrapper, mock } = await mountView(problem('Server said no', 500))
    expect(wrapper.text()).toContain('Server said no')

    await wrapper.get('[data-testid="client-groups-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.length).toBe(2)
  })

  it('creates a group through the dialog and closes it', async () => {
    const { wrapper, mock } = await mountView(json([]), json(7, 201), json([GROUP]))

    await wrapper.get('[data-testid="client-groups-add"]').trigger('click')
    const dialog = wrapper.getComponent(ClientGroupFormDialog)
    expect(dialog.props('open')).toBe(true)
    dialog.vm.$emit('save', { name: 'VIP', description: null, discountPercent: 10, color: '#112233' })
    await flushPromises()

    expect(callsOf(mock)[1]).toMatchObject({ method: 'POST', body: { name: 'VIP' } })
    expect(wrapper.getComponent(ClientGroupFormDialog).props('open')).toBe(false)
    expect(wrapper.find('[data-testid="client-group-row"]').exists()).toBe(true)
  })

  it('keeps the dialog open with the refusal when a save is refused', async () => {
    const { wrapper } = await mountView(json([]), problem('Name is taken.', 409))

    await wrapper.get('[data-testid="client-groups-add"]').trigger('click')
    wrapper.getComponent(ClientGroupFormDialog).vm.$emit('save', {
      name: 'VIP',
      description: null,
      discountPercent: 0,
      color: '#112233',
    })
    await flushPromises()

    expect(wrapper.getComponent(ClientGroupFormDialog).props('open')).toBe(true)
    expect(wrapper.getComponent(ClientGroupFormDialog).props('error')).toBe('Name is taken.')
  })

  it('asks before deleting and deletes on confirm', async () => {
    const { wrapper, mock } = await mountView(json([GROUP]), new Response(null, { status: 204 }), json([]))

    await wrapper.get('[data-testid="client-group-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="client-group-delete-1"]').trigger('click')
    const confirm = confirmDialog(wrapper)
    expect(confirm.props('message')).toContain('VIP')
    expect(callsOf(mock)).toHaveLength(1)

    confirm.vm.$emit('confirm')
    await flushPromises()

    expect(callsOf(mock)[1]).toMatchObject({ method: 'DELETE', url: '/api/admin/client-groups/1' })
    expect(wrapper.findAllComponents(UiDialog).some(dialog => dialog.props('variant') === 'confirm')).toBe(false)
  })

  it('shows the sentence of a refused delete', async () => {
    const { wrapper } = await mountView(json([GROUP]), problem('Group still has clients.', 409))

    await wrapper.get('[data-testid="client-group-actions-1"]').trigger('click')
    await wrapper.get('[data-testid="client-group-delete-1"]').trigger('click')
    confirmDialog(wrapper).vm.$emit('confirm')
    await flushPromises()

    expect(wrapper.get('[data-testid="client-groups-action-error"]').text()).toContain('Group still has clients.')
  })
})
