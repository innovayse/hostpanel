/**
 * Unit tests for SupportTicketPriorityBadge: each priority shows its translated label and tone.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SupportTicketPriorityBadge from '@/components/support/SupportTicketPriorityBadge.vue'
import { TicketPriority } from '@/types/ticket'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the badge for a priority.
 *
 * @param priority - The priority to show.
 * @returns The wrapper.
 */
const mountBadge = (priority: TicketPriority) =>
  mount(SupportTicketPriorityBadge, { props: { priority }, global: { plugins: [createTestI18n()] } })

describe('SupportTicketPriorityBadge', () => {
  it('shows the translated label of the priority', () => {
    expect(mountBadge(TicketPriority.Low).text()).toBe('Low')
    expect(mountBadge(TicketPriority.High).text()).toBe('High')
  })

  it('colours a high priority as a danger', () => {
    expect(mountBadge(TicketPriority.High).classes().join(' ')).toContain('text-status-red')
  })
})
