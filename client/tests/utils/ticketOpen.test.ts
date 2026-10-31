/**
 * Unit tests for `utils/ticketOpen.ts`: a ticket is open until it is closed.
 *
 * @module tests/utils/ticketOpen
 */

import { describe, expect, it } from 'vitest'
import { TicketStatus } from '~/types/ticketstatus'
import { isTicketOpen } from '~/utils/ticketOpen'

describe('isTicketOpen', () => {
  it('is true for every status but Closed', () => {
    expect(isTicketOpen({ status: TicketStatus.Open })).toBe(true)
    expect(isTicketOpen({ status: TicketStatus.AwaitingReply })).toBe(true)
    expect(isTicketOpen({ status: TicketStatus.Answered })).toBe(true)
  })

  it('is false for a closed ticket', () => {
    expect(isTicketOpen({ status: TicketStatus.Closed })).toBe(false)
  })
})
