/**
 * Unit tests for the action kind map: every kind has an icon, and a control's own icon wins over its kind's.
 */
import { describe, expect, it } from 'vitest'
import { Icon } from '@/lib/icons'
import { actionIcons, resolveActionIcon, resolveActionVariant, UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'

describe('actionIcons', () => {
  it('gives every kind an icon', () => {
    expect(Object.keys(actionIcons).sort()).toEqual(Object.values(UiActionKind).sort())
  })

  it('draws the common actions the same way everywhere', () => {
    expect(actionIcons[UiActionKind.Save]).toBe(Icon.Check)
    expect(actionIcons[UiActionKind.Delete]).toBe(Icon.Trash)
    expect(actionIcons[UiActionKind.Add]).toBe(Icon.Add)
    expect(actionIcons[UiActionKind.AddRow]).toBe(Icon.Add)
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

describe('resolveActionVariant', () => {
  it('always draws a create action primary, whatever look was asked for', () => {
    expect(resolveActionVariant(UiButtonVariant.Secondary, UiActionKind.Add)).toBe(UiButtonVariant.Primary)
    expect(resolveActionVariant(UiButtonVariant.Link, UiActionKind.Add)).toBe(UiButtonVariant.Primary)
    expect(resolveActionVariant(UiButtonVariant.Primary, UiActionKind.Add)).toBe(UiButtonVariant.Primary)
  })

  it('keeps the asked look for every other action, an added row included', () => {
    expect(resolveActionVariant(UiButtonVariant.Secondary, UiActionKind.AddRow)).toBe(UiButtonVariant.Secondary)
    expect(resolveActionVariant(UiButtonVariant.Danger, UiActionKind.Delete)).toBe(UiButtonVariant.Danger)
    expect(resolveActionVariant(UiButtonVariant.Ghost, undefined)).toBe(UiButtonVariant.Ghost)
  })
})
