/**
 * Stepping arithmetic shared by the kit's number fields (`UiNumberInput`, `UiStepper`).
 *
 * One place decides what a step button does: move the value by one step in a direction, rounded to
 * the step's precision, refused when it would leave the `min`/`max` range.
 */

/** Closed set of step directions. */
export const StepDirection = {
  Up: 1,
  Down: -1,
} as const

/** One of the step directions. */
export type StepDirection = (typeof StepDirection)[keyof typeof StepDirection]

/** Fewest decimals a stepped value is rounded to, so floating point drift never shows. */
const MIN_DECIMALS = 2

/**
 * Rounds a value to the step's precision (at least two decimals).
 *
 * @param value - Value to round.
 * @param step - The step the value moves by.
 * @returns The rounded value.
 */
const roundToStep = (value: number, step: number): number => {
  const decimals = (step.toString().split('.')[1] || '').length
  return parseFloat(value.toFixed(Math.max(decimals, MIN_DECIMALS)))
}

/**
 * Computes the value one step away from the current one.
 *
 * @param current - The value now.
 * @param step - Amount added or removed per step.
 * @param direction - Whether to raise or lower the value.
 * @param min - Lowest allowed value, if any.
 * @param max - Highest allowed value, if any.
 * @returns The stepped value, or `null` when the step would leave the range.
 */
export const stepValue = (
  current: number,
  step: number,
  direction: StepDirection,
  min?: number,
  max?: number,
): number | null => {
  const next = roundToStep(current + direction * step, step)
  if (min !== undefined && next < min) {
    return null
  }
  if (max !== undefined && next > max) {
    return null
  }
  return next
}
