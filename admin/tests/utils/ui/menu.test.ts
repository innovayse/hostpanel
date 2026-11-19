/**
 * Unit tests for the actions menu helpers: link or button, tone and element attributes.
 */
import { describe, expect, it } from 'vitest'
import { Icon } from '@/lib/icons'
import { elementAttrs, isLink, itemRole, toneClasses } from '@/utils/ui/menu'
import type { UiMenuAction } from '@/utils/ui/menu'

/**
 * Builds an action the way a screen does, with the icon every action carries.
 *
 * @param rest - What the case under test sets besides the icon.
 * @returns The action.
 */
const action = (rest: Omit<UiMenuAction, 'icon'>): UiMenuAction => ({ icon: Icon.Edit, ...rest })

describe('menu helpers', () => {
  it('treats an enabled action with a route as a link', () => {
    expect(isLink(action({ label: 'Open', to: '/x' }))).toBe(true)
    expect(isLink(action({ label: 'Open', to: '/x', disabled: true }))).toBe(false)
    expect(isLink(action({ label: 'Open' }))).toBe(false)
  })

  it('colours a destructive action red and the rest primary', () => {
    expect(toneClasses(action({ label: 'Delete', danger: true }))).toBe('text-status-red')
    expect(toneClasses(action({ label: 'Edit' }))).toBe('text-text-primary')
  })

  it('makes a checkable action a radio item and any other a plain item', () => {
    expect(itemRole(action({ label: 'Light', checked: false }))).toBe('menuitemradio')
    expect(itemRole(action({ label: 'Dark', checked: true }))).toBe('menuitemradio')
    expect(itemRole(action({ label: 'Edit' }))).toBe('menuitem')
  })

  it('gives a link its route and a button its type and disabled state', () => {
    expect(elementAttrs(action({ label: 'Open', to: '/x' }))).toEqual({ to: '/x' })
    expect(elementAttrs(action({ label: 'Edit', disabled: true }))).toEqual({ type: 'button', disabled: true })
  })
})
