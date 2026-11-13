/**
 * Unit tests for the button vocabulary: every look has classes, and `plain` adds none.
 */
import { describe, expect, it } from 'vitest'
import { buttonBaseClasses, buttonRadiusClasses, buttonVariantClasses, iconTriggerClasses, UiButtonVariant } from '@/utils/ui/button'

describe('button variants', () => {
  it('keeps the named looks', () => {
    expect(Object.values(UiButtonVariant)).toEqual(['plain', 'primary', 'secondary', 'ghost', 'field', 'danger', 'link'])
  })

  it('maps every look, with plain empty and the rest non-empty', () => {
    for (const variant of Object.values(UiButtonVariant)) {
      if (variant === UiButtonVariant.Plain) {
        expect(buttonVariantClasses[variant]).toBe('')
      } else {
        expect(buttonVariantClasses[variant].length).toBeGreaterThan(0)
      }
    }
  })

  it('draws the field look as a pill on the input-field surface, from tokens only', () => {
    expect(buttonVariantClasses.field).not.toContain('rounded')
    expect(buttonVariantClasses.field).toContain('bg-surface-field')
    expect(buttonVariantClasses.field).not.toMatch(/#[0-9a-f]{3,6}/i)
  })

  it('takes the radius from the size, except the field look which is always a pill', () => {
    expect(buttonRadiusClasses(UiButtonVariant.Primary, 'sm')).toBe('rounded-[9px]')
    expect(buttonRadiusClasses(UiButtonVariant.Primary, 'md')).toBe('rounded-control')
    expect(buttonRadiusClasses(UiButtonVariant.Field, 'sm')).toBe('rounded-pill')
  })

  it('shares a base set with a focus ring and a not-allowed cursor', () => {
    expect(buttonBaseClasses).toContain('focus-visible:ring-2')
    expect(buttonBaseClasses).toContain('focus-visible:ring-text-accent')
    expect(buttonBaseClasses).toContain('disabled:cursor-not-allowed')
  })

  it('gives every look a disabled colour set and no opacity-only dimming', () => {
    for (const variant of Object.values(UiButtonVariant)) {
      if (variant !== UiButtonVariant.Plain) {
        expect(buttonVariantClasses[variant]).toContain('disabled:text-text-muted')
        expect(buttonVariantClasses[variant]).not.toContain('opacity')
      }
    }
    expect(buttonBaseClasses).not.toContain('opacity')
  })

  it('applies hover and the gradient only while the control is not disabled', () => {
    expect(buttonVariantClasses.primary).toContain('not-disabled:gradient-brand')
    expect(buttonVariantClasses.primary).toContain('not-disabled:shadow-glow-primary')
    expect(buttonVariantClasses.primary).toContain('text-text-on-primary')
    expect(buttonVariantClasses.primary).toContain('not-disabled:hover:bg-(image:--gradient-primary-hover)')
    expect(buttonVariantClasses.primary).toContain('not-disabled:active:bg-(image:--gradient-primary-active)')
    for (const variant of [UiButtonVariant.Secondary, UiButtonVariant.Ghost, UiButtonVariant.Field, UiButtonVariant.Danger, UiButtonVariant.Link]) {
      expect(buttonVariantClasses[variant]).not.toMatch(/(^|s)hover:/)
    }
  })
})

describe('icon trigger look', () => {
  it('has the 42px footprint and the ghost colours of the icon button', () => {
    expect(iconTriggerClasses).toContain('h-10.5')
    expect(iconTriggerClasses).toContain('min-w-10.5')
    expect(iconTriggerClasses).toContain('text-text-secondary')
    expect(iconTriggerClasses).toContain('group-hover:bg-surface-hover')
  })
})
