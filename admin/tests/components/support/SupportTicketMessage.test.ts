/**
 * Unit tests for SupportTicketMessage: the sender line, the staff badge and the side of the bubble.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SupportTicketMessage from '@/components/support/SupportTicketMessage.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts a message.
 *
 * @param staff - Whether a staff member wrote it.
 * @returns The wrapper.
 */
const mountMessage = (staff: boolean) =>
  mount(SupportTicketMessage, {
    props: { author: 'Ada Lovelace', createdAt: '2026-10-14T09:30:00Z', staff },
    slots: { default: 'The site is back.' },
    global: { plugins: [createTestI18n()] },
  })

describe('SupportTicketMessage', () => {
  it('shows the author, the time and the message text', () => {
    const wrapper = mountMessage(false)

    expect(wrapper.get('[data-testid="ticket-message-author"]').text()).toBe('Ada Lovelace')
    expect(wrapper.text()).toContain('/2026')
    expect(wrapper.text()).toContain('The site is back.')
  })

  it('puts a staff message on the own side with a Staff badge', () => {
    const wrapper = mountMessage(true)

    expect(wrapper.get('[data-side="own"] [data-testid="ui-chat-bubble-body"]').text()).toBe('The site is back.')
    expect(wrapper.text()).toContain('Staff')
  })

  it('puts a client message on the other side without a badge', () => {
    const wrapper = mountMessage(false)

    expect(wrapper.get('[data-side="other"] [data-testid="ui-chat-bubble-body"]').text()).toBe('The site is back.')
    expect(wrapper.text()).not.toContain('Staff')
  })
})
