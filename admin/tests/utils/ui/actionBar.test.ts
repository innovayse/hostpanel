/**
 * Unit tests for the bar action vocabulary: which action stays a button on a phone and how a folded one becomes a menu row.
 */
import { describe, expect, it } from 'vitest'
import { Icon } from '@/lib/icons'
import { barActionToMenu, drawHeaderAction, headerAction, primaryIndex } from '@/utils/ui/actionBar'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiBarAction } from '@/utils/ui/actionBar'
import { UiButtonVariant } from '@/utils/ui/button'

/**
 * Builds an action in a look.
 *
 * @param variant - The button look.
 * @returns The action.
 */
const action = (variant: UiButtonVariant): UiBarAction => ({ label: 'L', icon: Icon.Check, variant, testid: 't' })

describe('primaryIndex', () => {
  it('picks the first action in the primary look', () => {
    expect(primaryIndex([action(UiButtonVariant.Secondary), action(UiButtonVariant.Primary), action(UiButtonVariant.Primary)])).toBe(1)
  })

  it('falls back to the first action, and to -1 when there are none', () => {
    expect(primaryIndex([action(UiButtonVariant.Secondary), action(UiButtonVariant.Danger)])).toBe(0)
    expect(primaryIndex([])).toBe(-1)
  })
})

describe('barActionToMenu', () => {
  it('disables the row of an action that is loading, as its button is', () => {
    expect(barActionToMenu({ ...action(UiButtonVariant.Primary), loading: true }).disabled).toBe(true)
    expect(barActionToMenu({ ...action(UiButtonVariant.Primary), loading: false }).disabled).toBe(false)
  })

  it('names the row by the full sentence when there is one, else by the label', () => {
    expect(barActionToMenu({ ...action(UiButtonVariant.Secondary), title: 'Mark as paid' }).label).toBe('Mark as paid')
    expect(barActionToMenu(action(UiButtonVariant.Secondary)).label).toBe('L')
  })

  it('carries the icon, the danger tone, the disabled state and the test id', () => {
    const row = barActionToMenu({ ...action(UiButtonVariant.Danger), disabled: true, testid: 'x' })

    expect(row).toEqual({ label: 'L', icon: Icon.Check, danger: true, disabled: true, testid: 'x' })
    expect(barActionToMenu(action(UiButtonVariant.Primary)).danger).toBe(false)
  })
})

describe('headerAction', () => {
  it('keeps the kind and everything else of the action as given', () => {
    const built = headerAction(UiActionKind.Add, { label: 'L', variant: UiButtonVariant.Primary, testid: 't', to: '/add', loading: true })

    expect(built).toEqual({ label: 'L', variant: UiButtonVariant.Primary, testid: 't', to: '/add', loading: true, kind: UiActionKind.Add })
  })
})

describe('drawHeaderAction', () => {
  it.each(Object.values(UiActionKind))('draws the %s kind with the icon the map gives it', (kind) => {
    expect(drawHeaderAction(headerAction(kind, { label: 'L', variant: UiButtonVariant.Secondary, testid: 't' })).icon).toBe(actionIcons[kind])
  })
})
