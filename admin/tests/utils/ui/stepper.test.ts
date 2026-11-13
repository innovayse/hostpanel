/**
 * Unit tests for the shared stepping arithmetic: direction, rounding to the step's precision and
 * refusal at the range edges.
 */
import { describe, expect, it } from 'vitest'
import { StepDirection, stepValue } from '@/utils/ui/stepper'

describe('stepValue', () => {
  it('moves the value one step up or down', () => {
    expect(stepValue(5, 2, StepDirection.Up)).toBe(7)
    expect(stepValue(5, 2, StepDirection.Down)).toBe(3)
  })

  it('rounds away floating point drift', () => {
    expect(stepValue(0.1, 0.2, StepDirection.Up)).toBe(0.3)
  })

  it('allows a step that lands exactly on a bound', () => {
    expect(stepValue(4, 1, StepDirection.Up, 0, 5)).toBe(5)
    expect(stepValue(1, 1, StepDirection.Down, 0, 5)).toBe(0)
  })

  it('returns null when the step would leave the range', () => {
    expect(stepValue(5, 1, StepDirection.Up, 0, 5)).toBeNull()
    expect(stepValue(0, 1, StepDirection.Down, 0, 5)).toBeNull()
  })

  it('has no bounds unless given', () => {
    expect(stepValue(0, 1, StepDirection.Down)).toBe(-1)
  })
})
