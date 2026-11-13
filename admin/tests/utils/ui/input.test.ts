/**
 * Unit tests for the field vocabulary: the shared class sets.
 */
import { describe, expect, it } from 'vitest'
import {
  fieldBaseClasses,
  fieldInvalidClasses,
  fieldSurfaceClasses,
  fieldWidthClasses,
  focusRingClasses,
  inputAutocomplete,
  UiInputType,
} from '@/utils/ui/input'

describe('field vocabulary', () => {
  it('applies width through :where() so a screen class always wins', () => {
    expect(fieldWidthClasses).toContain(':where(&)')
  })

  it('reads the theme tokens, and the invalid set swaps the border to red', () => {
    expect(fieldBaseClasses).toContain('bg-surface-field')
    expect(fieldBaseClasses).toContain('border-border-control')
    expect(fieldInvalidClasses).toContain('border-status-red')
  })
})

describe('field focus look', () => {
  it('draws a keyboard-only ring at 30% instead of a 10% wash', () => {
    expect(fieldBaseClasses).toContain('focus-visible:ring-text-accent/40')
    expect(focusRingClasses).toContain('focus-visible:ring-text-accent')
    expect(fieldInvalidClasses).toContain('focus-visible:ring-status-red/30')
  })
})

describe('field disabled look and autofill hints', () => {
  it('mutes a disabled field by colour and cursor, not by opacity alone', () => {
    expect(fieldBaseClasses).toContain('disabled:text-text-muted')
    expect(fieldBaseClasses).toContain('disabled:cursor-not-allowed')
    expect(fieldBaseClasses).not.toContain('opacity')
  })

  it('keeps the radius out of the surface so a joined field can set its own corners', () => {
    expect(fieldSurfaceClasses).not.toContain('rounded')
    expect(fieldBaseClasses).toContain(fieldSurfaceClasses)
  })

  it('uses the control radius token', () => {
    expect(fieldBaseClasses).toContain('rounded-control')
  })

  it('hints autofill for email, password, tel and url only', () => {
    expect(inputAutocomplete[UiInputType.Email]).toBe('email')
    expect(inputAutocomplete[UiInputType.Password]).toBe('current-password')
    expect(inputAutocomplete[UiInputType.Tel]).toBe('tel')
    expect(inputAutocomplete[UiInputType.Url]).toBe('url')
    expect(inputAutocomplete[UiInputType.Text]).toBeUndefined()
  })
})
