/**
 * Unit tests for the button vocabulary: every look has classes.
 */
import { describe, expect, it } from 'vitest'
import { buttonBaseClasses, buttonRadiusClasses, buttonVariantClasses, iconTriggerClasses, UiButtonVariant } from '@/utils/ui/button'

describe('button variants', () => {
  it('keeps the named looks', () => {
    expect(Object.values(UiButtonVariant)).toEqual(['primary', 'secondary', 'ghost', 'danger', 'link'])
  })

  it('maps every look to classes', () => {
    for (const variant of Object.values(UiButtonVariant)) {
      expect(buttonVariantClasses[variant].length).toBeGreaterThan(0)
    }
  })

  it('takes the radius from the size, (a look never changes it)', () => {
    expect(buttonRadiusClasses('sm')).toBe('rounded-[9px]')
    expect(buttonRadiusClasses('md')).toBe('rounded-control')
  })

  it('draws a round control at the smallest size', () => {
    expect(buttonRadiusClasses('xs')).toBe('rounded-full')
  })

  it('shares a base set with a focus ring and a not-allowed cursor', () => {
    expect(buttonBaseClasses).toContain('focus-visible:ring-2')
    expect(buttonBaseClasses).toContain('focus-visible:ring-text-accent')
    expect(buttonBaseClasses).toContain('disabled:cursor-not-allowed')
  })

  it('gives every look a disabled colour set and no opacity-only dimming', () => {
    for (const variant of Object.values(UiButtonVariant)) {
      expect(buttonVariantClasses[variant]).toContain('disabled:text-text-muted')
      expect(buttonVariantClasses[variant]).not.toContain('opacity')
    }
    expect(buttonBaseClasses).not.toContain('opacity')
  })

  it('applies hover and the gradient only while the control is not disabled', () => {
    expect(buttonVariantClasses.primary).toContain('not-disabled:gradient-brand')
    expect(buttonVariantClasses.primary).toContain('not-disabled:shadow-glow-primary')
    expect(buttonVariantClasses.primary).toContain('text-text-on-primary')
    expect(buttonVariantClasses.primary).toContain('not-disabled:hover:bg-(image:--gradient-primary-hover)')
    expect(buttonVariantClasses.primary).toContain('not-disabled:active:bg-(image:--gradient-primary-active)')
    for (const variant of [UiButtonVariant.Secondary, UiButtonVariant.Ghost, UiButtonVariant.Danger, UiButtonVariant.Link]) {
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
