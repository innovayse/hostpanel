/**
 * Unit tests for the media queries that mirror Tailwind's `max-` variants.
 */
import { describe, expect, it } from 'vitest'
import { UI_BELOW_LG_QUERY, UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'

describe('breakpoint queries', () => {
  it('are the max-width queries just under the sm (640 px) and lg (1024 px) breakpoints', () => {
    expect(UI_BELOW_SM_QUERY).toBe('(max-width: 639.98px)')
    expect(UI_BELOW_LG_QUERY).toBe('(max-width: 1023.98px)')
  })
})
