/**
 * Unit tests for the action kind map: every kind has an icon, and a control's own icon wins over its kind's.
 */
import { describe, expect, it } from 'vitest'
import { Icon } from '@/lib/icons'
import { actionIcons, resolveActionIcon, UiActionKind } from '@/utils/ui/actions'

describe('actionIcons', () => {
  it('gives every kind an icon', () => {
    expect(Object.keys(actionIcons).sort()).toEqual(Object.values(UiActionKind).sort())
  })

  it('draws the common actions the same way everywhere', () => {
    expect(actionIcons[UiActionKind.Save]).toBe(Icon.Check)
    expect(actionIcons[UiActionKind.Delete]).toBe(Icon.Trash)
    expect(actionIcons[UiActionKind.Add]).toBe(Icon.Add)
    expect(actionIcons[UiActionKind.Edit]).toBe(Icon.Edit)
    expect(actionIcons[UiActionKind.Print]).toBe(Icon.Printer)
    expect(actionIcons[UiActionKind.Generate]).toBe(Icon.Play)
  })
})

describe('resolveActionIcon', () => {
  it('takes the icon of the kind', () => {
    expect(resolveActionIcon(undefined, UiActionKind.Save)).toBe(Icon.Check)
  })

  it('prefers the control own icon', () => {
    expect(resolveActionIcon(Icon.Mail, UiActionKind.Save)).toBe(Icon.Mail)
  })

  it('answers nothing when the control names neither', () => {
    expect(resolveActionIcon(undefined, undefined)).toBeUndefined()
  })
})
