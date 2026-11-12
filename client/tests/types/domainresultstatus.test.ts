/**
 * Unit tests for the domain availability states: unknown is its own state, never folded into taken.
 *
 * @module tests/types/domainresultstatus.test
 */
import { describe, expect, it } from 'vitest'
import { DomainResultStatus } from '~/types/domainresultstatus'

describe('DomainResultStatus', () => {
  it('has exactly the three states the registrar check can report', () => {
    expect(Object.values(DomainResultStatus)).toEqual(['available', 'taken', 'unknown'])
  })

  it('keeps unknown distinct from taken', () => {
    expect(DomainResultStatus.Unknown).not.toBe(DomainResultStatus.Taken)
  })
})
