/**
 * Tests for the enabled-state filter helper: the choice a list screen reads from its address becomes the API's switch.
 */
import { describe, expect, it } from 'vitest'
import { enabledOfFilter } from '@/lib/enabledFilter'
import { EnabledFilter } from '@/types/enabledfilter'

describe('enabledOfFilter', () => {
  it('turns the enabled choice into true', () => {
    expect(enabledOfFilter(EnabledFilter.Enabled)).toBe(true)
  })

  it('turns the disabled choice into false', () => {
    expect(enabledOfFilter(EnabledFilter.Disabled)).toBe(false)
  })

  it('leaves the list unnarrowed when no choice is set', () => {
    expect(enabledOfFilter(undefined)).toBeUndefined()
  })
})
