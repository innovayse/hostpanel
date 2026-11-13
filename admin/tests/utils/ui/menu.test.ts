/**
 * Unit tests for the actions menu helpers: link or button, tone and element attributes.
 */
import { describe, expect, it } from 'vitest'
import { elementAttrs, isLink, itemRole, toneClasses } from '@/utils/ui/menu'

describe('menu helpers', () => {
  it('treats an enabled action with a route as a link', () => {
    expect(isLink({ label: 'Open', to: '/x' })).toBe(true)
    expect(isLink({ label: 'Open', to: '/x', disabled: true })).toBe(false)
    expect(isLink({ label: 'Open' })).toBe(false)
  })

  it('colours a destructive action red and the rest primary', () => {
    expect(toneClasses({ label: 'Delete', danger: true })).toBe('text-status-red')
    expect(toneClasses({ label: 'Edit' })).toBe('text-text-primary')
  })

  it('makes a checkable action a radio item and any other a plain item', () => {
    expect(itemRole({ label: 'Light', checked: false })).toBe('menuitemradio')
    expect(itemRole({ label: 'Dark', checked: true })).toBe('menuitemradio')
    expect(itemRole({ label: 'Edit' })).toBe('menuitem')
  })

  it('gives a link its route and a button its type and disabled state', () => {
    expect(elementAttrs({ label: 'Open', to: '/x' })).toEqual({ to: '/x' })
    expect(elementAttrs({ label: 'Edit', disabled: true })).toEqual({ type: 'button', disabled: true })
  })
})
