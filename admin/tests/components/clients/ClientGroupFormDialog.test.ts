/**
 * Mount tests for ClientGroupFormDialog: the form starts empty for a new group and from the group
 * being edited, reports the finished payload, blocks a nameless save and shows the API's messages.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ClientGroupFormDialog from '@/components/clients/ClientGroupFormDialog.vue'
import type { ClientGroup } from '@/types/clientgroup'
import { createTestI18n } from '../../helpers/i18n'

/** A group being edited. */
const GROUP: ClientGroup = {
  id: 3,
  name: 'VIP',
  description: 'Top clients',
  discountPercent: 12.5,
  color: '#112233',
  isDefault: false,
  clientCount: 4,
}

/**
 * Mounts the dialog.
 *
 * @param props - Props that replace the defaults.
 * @returns The wrapper.
 */
const mountDialog = (props: Record<string, unknown> = {}) =>
  mount(ClientGroupFormDialog, {
    props: { open: true, group: null, saving: false, error: null, fieldErrors: {}, ...props },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('ClientGroupFormDialog', () => {
  it('disables Save while the name is empty', () => {
    const wrapper = mountDialog()

    expect(wrapper.get('[data-testid="client-group-form-save"]').attributes('disabled')).toBeDefined()
  })

  it('reports the typed fields as the payload', async () => {
    const wrapper = mountDialog()

    await wrapper.get('[data-testid="client-group-form-name"]').setValue('  Resellers ')
    await wrapper.get('[data-testid="client-group-form-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([[{ name: 'Resellers', description: null, discountPercent: 0, color: '#3B82F6' }]])
  })

  it('starts from the group being edited', async () => {
    const wrapper = mountDialog({ group: GROUP })

    await wrapper.get('[data-testid="client-group-form-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([[{ name: 'VIP', description: 'Top clients', discountPercent: 12.5, color: '#112233' }]])
  })

  it('shows the refusal sentence and the field message', () => {
    const wrapper = mountDialog({ error: 'Refused.', fieldErrors: { name: 'Name is taken.' } })

    expect(wrapper.text()).toContain('Refused.')
    expect(wrapper.text()).toContain('Name is taken.')
  })

  it('emits close from Cancel', async () => {
    const wrapper = mountDialog()

    await wrapper.get('[data-testid="client-group-form-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
