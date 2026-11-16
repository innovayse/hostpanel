/**
 * Mount tests for GroupFormModal: a new group starts empty, an edited group is loaded, servers
 * move between the two lists, save emits the payload, and the API's sentences are shown.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import GroupFormModal from '@/components/servers/GroupFormModal.vue'
import { createTestI18n } from '../../helpers/i18n'
import { serverDto, serverGroupDto, serverOptionDto } from '../../helpers/serverFixtures'

/** Two servers to choose from. */
const servers = [serverOptionDto({ id: 1, name: 'host1' }), serverOptionDto({ id: 2, name: 'host2' })]

/**
 * Mounts the dialog with the teleport inlined.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountModal = (props: Record<string, unknown> = {}) =>
  mount(GroupFormModal, {
    props: { group: null, availableServers: servers, saving: false, ...props },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('GroupFormModal', () => {
  it('starts empty when creating, with every server available', () => {
    const wrapper = mountModal()

    expect(wrapper.text()).toContain('Create New Group')
    expect(wrapper.find('[data-testid="group-form-add-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="group-form-add-2"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('None selected')
  })

  it('loads an edited group with its servers in the group list', () => {
    const wrapper = mountModal({ group: serverGroupDto({ servers: [serverDto({ id: 1, name: 'host1' })] }) })

    expect(wrapper.text()).toContain('Edit Group')
    expect(wrapper.find('[data-testid="group-form-remove-1"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="group-form-add-2"]').exists()).toBe(true)
  })

  it('moves a server into the group and emits the payload on save', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="group-form-name"]').setValue('Reseller')
    await wrapper.get('[data-testid="group-form-add-2"]').trigger('click')
    await wrapper.get('[data-testid="group-form-save"]').trigger('click')

    expect(wrapper.emitted('saved')).toEqual([[{ name: 'Reseller', fillType: 'LeastFull', serverIds: [2] }]])
  })

  it('shows the refusal and the field sentence the API returned', () => {
    const wrapper = mountModal({ error: 'Group was refused.', fieldErrors: { name: 'Name is taken.' } })

    expect(wrapper.get('[data-testid="group-form-error"]').text()).toContain('Group was refused.')
    expect(wrapper.text()).toContain('Name is taken.')
  })

  it('emits close from cancel', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="group-form-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
