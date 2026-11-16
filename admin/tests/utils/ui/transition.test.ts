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

  it('clips the collapsing block only while it moves, so its focus rings are not cut when it is open', () => {
    const set = transitionSet[UiTransitionKind.Collapse]

    expect(tokens(set.enterActiveClass)).toContain('ui-collapsing')
    expect(tokens(set.leaveActiveClass)).toContain('ui-collapsing')
    expect(tokens(set.enterToClass)).not.toContain('ui-collapsing')
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

  it('gives a modal panel the same duration token as its overlay, since Vue times the transition from the overlay only', () => {
    /**
     * Reads the duration token of a class string.
     *
     * @param classes - A class string with exactly one duration class.
     * @returns The token name, such as `base`.
     */
    const durationOf = (classes: string): string => {
      const found = tokens(classes).filter(token => token.startsWith('duration-'))
      expect(found).toHaveLength(1)
      return (found[0] ?? '').replace('duration-(--duration-', '').replace(')', '')
    }
    const pairs: [UiTransitionKind, string[]][] = [
      [UiTransitionKind.Dialog, [modalPanelClasses.dialog]],
      [UiTransitionKind.Drawer, [modalPanelClasses.drawerLeft, modalPanelClasses.drawerRight, modalPanelClasses.sheet]],
    ]
    for (const [kind, panels] of pairs) {
      const enter = durationOf(transitionSet[kind].enterActiveClass)
      expect(durationOf(transitionSet[kind].leaveActiveClass)).toBe(enter)
      for (const panel of panels) {
        expect(durationOf(panel)).toBe(enter)
      }
    }
  })

  it('removes the panel transform under reduced motion too', () => {
    for (const panel of Object.values(modalPanelClasses)) {
      const moving = tokens(panel).filter(token => /^group-\[\.ui-from\]\/overlay:-?(?:translate-[xy]|scale)/.test(token))
      expect(moving.length).toBe(1)
      expect(tokens(panel).some(token => token.startsWith('motion-reduce:group-[.ui-from]/overlay:'))).toBe(true)
    }
  })
})

describe('modalPanelClasses.sheet', () => {
  it('rises from the bottom edge on the slow token and stands still under reduced motion', () => {
    const list = tokens(modalPanelClasses.sheet)

    expect(list).toContain('group-[.ui-from]/overlay:translate-y-full')
    expect(list).toContain('duration-(--duration-slow)')
    expect(list).toContain('motion-reduce:transition-none')
    expect(list).toContain('motion-reduce:group-[.ui-from]/overlay:translate-y-0')
  })
})
