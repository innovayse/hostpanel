/**
 * Unit tests for the shared transition set: every kind has its six classes as full literals on the named duration
 * tokens, and nothing in it moves under reduced motion.
 */
import { describe, expect, it } from 'vitest'
import { modalPanelClasses, transitionSet, UiTransitionKind } from '@/utils/ui/transition'
import type { UiTransitionClasses } from '@/utils/ui/transition'

/**
 * Splits a class string into its classes.
 *
 * @param classes - A class string.
 * @returns The classes.
 */
const tokens = (classes: string): string[] => classes.split(/\s+/).filter(Boolean)

/**
 * Collects every class of a set of Vue transition classes.
 *
 * @param set - The six classes of one kind.
 * @returns Every class, with repeats.
 */
const allTokens = (set: UiTransitionClasses): string[] => Object.values(set).flatMap(tokens)

/** The classes that move something: a transform, written as an individual property in Tailwind. */
const TRANSFORM = /^(?:-?translate-[xy]|scale|rotate)/

describe('transitionSet', () => {
  it('has the six classes for every kind', () => {
    expect(Object.keys(transitionSet).sort()).toEqual(Object.values(UiTransitionKind).sort())
    for (const set of Object.values(transitionSet)) {
      for (const classes of Object.values(set)) {
        expect(tokens(classes).length).toBeGreaterThan(0)
      }
    }
  })

  it('takes every duration from the named tokens, never a bare number', () => {
    const everything = [...Object.values(transitionSet).flatMap(allTokens), ...Object.values(modalPanelClasses).flatMap(tokens)]
    const durations = everything.filter(token => token.includes('duration-'))
    expect(durations.length).toBeGreaterThan(0)
    for (const duration of durations) {
      expect(duration).toMatch(/duration-\(--duration-(?:fast|base|slow)\)$/)
    }
  })

  it('keeps dropdowns quick, at the fast token', () => {
    expect(transitionSet[UiTransitionKind.Dropdown].enterActiveClass).toContain('duration-(--duration-fast)')
    expect(transitionSet[UiTransitionKind.Dropdown].leaveActiveClass).toContain('duration-(--duration-fast)')
  })

  it('has no transition at all under reduced motion', () => {
    for (const set of Object.values(transitionSet)) {
      expect(set.enterActiveClass).toContain('motion-reduce:transition-none')
      expect(set.leaveActiveClass).toContain('motion-reduce:transition-none')
    }
    for (const panel of Object.values(modalPanelClasses)) {
      expect(panel).toContain('motion-reduce:transition-none')
    }
  })

  it('removes every transform under reduced motion', () => {
    for (const set of Object.values(transitionSet)) {
      const all = allTokens(set)
      const moving = all.filter(token => TRANSFORM.test(token))
      for (const token of moving) {
        const axis = token.replace(/^-/, '').replace(/-.*$/, '')
        expect(all.some(other => other.startsWith(`motion-reduce:${axis}`) || other.startsWith(`motion-reduce:-${axis}`))).toBe(true)
      }
    }
  })

  it('removes the panel transform under reduced motion too', () => {
    for (const panel of Object.values(modalPanelClasses)) {
      const moving = tokens(panel).filter(token => /^group-\[\.ui-from\]\/overlay:-?(?:translate-x|scale)/.test(token))
      expect(moving.length).toBe(1)
      expect(tokens(panel).some(token => token.startsWith('motion-reduce:group-[.ui-from]/overlay:'))).toBe(true)
    }
  })
})
