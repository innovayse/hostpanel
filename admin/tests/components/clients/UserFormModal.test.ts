/**
 * Unit tests for UserFormModal: pre-filled fields, save payload, permissions, owner banner,
 * accounts table and the two-step delete.
 */
import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UserFormModal from '@/components/clients/UserFormModal.vue'
import { ClientPermission } from '@/types/clientuser'
import type { UserDetail } from '@/types/useraccount'
import { createTestI18n } from '../../helpers/i18n'

/** A user with one owned account. */
const user: UserDetail = {
  id: 'u1',
  clientId: 7,
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  language: 'en',
  lastLoginAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  accounts: [{ clientId: 7, firstName: 'Ada', lastName: 'Lovelace', companyName: 'Analytical', isOwner: true }],
}

/**
 * Mounts the modal with the teleport inlined so its content is queryable.
 *
 * @param props - Props for the modal.
 * @returns The wrapper.
 */
const mountModal = (props: Partial<InstanceType<typeof UserFormModal>['$props']> = {}) =>
  mount(UserFormModal, {
    props: { user, ...props },
    global: { plugins: [createTestI18n()], stubs: { teleport: true } },
  })

describe('UserFormModal', () => {
  it('pre-fills the form and titles the dialog with the email', async () => {
    const wrapper = mountModal()
    await flushPromises()

    expect(wrapper.get('h2').text()).toContain('ada@example.com')
    expect((wrapper.get('[data-testid="clients-userform-first-name"]').element as HTMLInputElement).value).toBe('Ada')
    expect((wrapper.get('[data-testid="clients-userform-email"]').element as HTMLInputElement).placeholder).not.toBe('')
  })

  it('emits the edited values on save', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-userform-last-name"]').setValue('Byron')
    await wrapper.get('[data-testid="clients-userform-save"]').trigger('click')

    expect(wrapper.emitted('save')?.[0]).toEqual([
      { firstName: 'Ada', lastName: 'Byron', email: 'ada@example.com', language: 'en' },
    ])
  })

  it('emits permissions and ownership for a non-owner with a client', async () => {
    const wrapper = mountModal({ clientId: 7, isOwner: false, permissions: ClientPermission.None })

    await wrapper.get(`[data-testid="clients-userform-permission-${ClientPermission.ViewDomains}"]`).trigger('click')
    await wrapper.get('[data-testid="clients-userform-make-owner"]').trigger('click')
    await wrapper.get('[data-testid="clients-userform-save"]').trigger('click')

    expect(wrapper.emitted('updatePermissions')?.[0]).toEqual([ClientPermission.ViewDomains])
    expect(wrapper.emitted('makeOwner')).toHaveLength(1)
  })

  it('shows the owner banner and hides the transfer checkbox for the owner', () => {
    const wrapper = mountModal({ clientId: 7, isOwner: true })

    expect(wrapper.find('[role="status"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="clients-userform-make-owner"]').exists()).toBe(false)
  })

  it('lists linked accounts, or says there are none', () => {
    expect(mountModal().get('[data-testid="clients-userform-accounts"]').text()).toContain('Analytical')
    expect(mountModal({ user: { ...user, accounts: [] } }).find('[data-testid="clients-userform-accounts"]').exists()).toBe(false)
  })

  it('shows ownership as a status icon and grows the name column', () => {
    const table = mountModal().get('[data-testid="clients-userform-accounts"]')

    expect(table.get('[role="img"]').attributes('aria-label')).toBe('Owner')
    expect(table.findAll('th').some((th) => th.classes().includes('max-w-0'))).toBe(true)
  })

  it('asks before deleting and emits delete only after confirming', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-userform-delete"]').trigger('click')
    expect(wrapper.emitted('delete')).toBeUndefined()
    expect(wrapper.text()).toContain('ada@example.com')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(wrapper.emitted('delete')).toHaveLength(1)
  })

  it('emits close from the footer button', async () => {
    const wrapper = mountModal()

    await wrapper.get('[data-testid="clients-userform-close"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
