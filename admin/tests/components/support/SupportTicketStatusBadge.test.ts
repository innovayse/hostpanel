/**
 * Unit tests for SupportTicketStatusBadge: each status shows its translated label and tone.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SupportTicketStatusBadge from '@/components/support/SupportTicketStatusBadge.vue'
import { TicketStatus } from '@/types/ticket'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the badge for a status.
 *
 * @param status - The status to show.
 * @returns The wrapper.
 */
const mountBadge = (status: TicketStatus) =>
  mount(SupportTicketStatusBadge, { props: { status }, global: { plugins: [createTestI18n()] } })

describe('SupportTicketStatusBadge', () => {
  it('shows the translated label of the status', () => {
    expect(mountBadge(TicketStatus.AwaitingReply).text()).toBe('Awaiting Reply')
    expect(mountBadge(TicketStatus.Closed).text()).toBe('Closed')
  })

  it('colours a waiting ticket as a warning', () => {
    expect(mountBadge(TicketStatus.AwaitingReply).classes().join(' ')).toContain('text-status-yellow')
  })
})
