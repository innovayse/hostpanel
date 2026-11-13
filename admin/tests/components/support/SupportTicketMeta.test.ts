/**
 * Unit tests for SupportTicketMeta: the details of a ticket, the save, the department retry and the
 * outcome line.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SupportTicketMeta from '@/components/support/SupportTicketMeta.vue'
import { TicketPriority, TicketStatus } from '@/types/ticket'
import type { Ticket } from '@/types/ticket'
import { UiTone } from '@/utils/ui/tone'
import { createTestI18n } from '../../helpers/i18n'

/** A ticket assigned to nobody, in the Awaiting Reply status. */
const ticket: Ticket = {
  id: 9,
  clientId: 3,
  subject: 'DNS is down',
  message: 'Help',
  status: TicketStatus.AwaitingReply,
  priority: TicketPriority.High,
  department: { id: 2, name: 'Support' },
  assignee: { id: null, name: null },
  createdAt: '2026-10-01T08:00:00Z',
  replies: [],
}

/**
 * Mounts the panel.
 *
 * @param props - Props that differ from the defaults.
 * @returns The wrapper.
 */
const mountMeta = (props: Record<string, unknown> = {}) =>
  mount(SupportTicketMeta, {
    props: {
      ticket,
      departmentOptions: [{ value: 2, label: 'Support' }],
      departmentsError: null,
      lastReplyAt: null,
      saving: false,
      departmentId: 2,
      priority: TicketPriority.High,
      ...props,
    },
    global: { plugins: [createTestI18n()] },
  })

describe('SupportTicketMeta', () => {
  it('keeps the captions and draws placeholders for the controls while the ticket loads', () => {
    const wrapper = mountMeta({ ticket: null })

    expect(wrapper.text()).toContain('Department')
    expect(wrapper.find('[data-testid="ticket-department"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ticket-assignee"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBe(6)
  })

  it('shows the status, the assignee, the creation time and that nobody has replied', () => {
    const wrapper = mountMeta()

    expect(wrapper.text()).toContain('Awaiting Reply')
    expect(wrapper.get('[data-testid="ticket-assignee"]').text()).toBe('Unassigned')
    expect(wrapper.get('[data-testid="ticket-created"]').text()).toContain('/2026')
    expect(wrapper.get('[data-testid="ticket-last-reply"]').text()).toBe('No replies yet')
  })

  it('asks the screen to save when Save is pressed', async () => {
    const wrapper = mountMeta()

    await wrapper.get('[data-testid="ticket-save"]').trigger('click')

    expect(wrapper.emitted('save')).toHaveLength(1)
  })

  it('shows the department sentence with a retry instead of the select', async () => {
    const wrapper = mountMeta({ departmentsError: 'Departments are unavailable.' })

    expect(wrapper.find('[data-testid="ticket-department"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="ticket-departments-error"]').text()).toBe('Departments are unavailable.')

    await wrapper.get('[data-testid="ticket-departments-error-retry"]').trigger('click')

    expect(wrapper.emitted('retryDepartments')).toHaveLength(1)
  })

  it('shows the outcome of the save in its tone', () => {
    const wrapper = mountMeta({ feedback: 'Refused.', feedbackTone: UiTone.Danger })

    const line = wrapper.get('[data-testid="ticket-save-feedback"]')
    expect(line.text()).toBe('Refused.')
    expect(line.attributes('data-tone')).toBe('danger')
  })
})
