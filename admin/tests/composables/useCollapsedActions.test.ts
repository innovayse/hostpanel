/**
 * Unit tests for useCollapsedActions: every action is a button from md up, and all of them are menu rows below it.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { useCollapsedActions } from '@/composables/useCollapsedActions'
import { Icon } from '@/lib/icons'
import type { UiBarAction } from '@/utils/ui/actionBar'
import { UiButtonVariant } from '@/utils/ui/button'

/** Two actions, the primary one second. */
const ACTIONS: UiBarAction[] = [
  { label: 'A', icon: Icon.Check, variant: UiButtonVariant.Secondary, testid: 'a' },
  { label: 'B', icon: Icon.Add, variant: UiButtonVariant.Primary, testid: 'b' },
]

/** The real media query lookup, restored after each test. */
const original = window.matchMedia

/**
 * Makes every media query answer the same.
 *
 * @param matches - Whether the screen counts as below `md`.
 */
const answerQueries = (matches: boolean): void => {
  window.matchMedia = ((query: string) => ({ matches, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
}

/**
 * Runs the composable inside a component, as a header does, so its media query listener has an owner.
 *
 * @param actions - The actions to split.
 * @returns What the composable returned.
 */
const collapse = (actions: UiBarAction[]): ReturnType<typeof useCollapsedActions<UiBarAction>> => {
  let result: ReturnType<typeof useCollapsedActions<UiBarAction>> | undefined
  mount(defineComponent({
    /**
     * Calls the composable.
     *
     * @returns An empty render function.
     */
    setup: () => {
      result = useCollapsedActions(() => actions, (action) => ({ label: action.label, icon: action.icon, testid: action.testid }))
      return () => null
    },
  }))
  return result as ReturnType<typeof useCollapsedActions<UiBarAction>>
}

describe('useCollapsedActions', () => {
  afterEach(() => {
    window.matchMedia = original
  })

  it('draws every action as a button and no menu rows from md up', () => {
    answerQueries(false)

    const { buttons, rows } = collapse(ACTIONS)

    expect(buttons.value.map((action) => action.testid)).toEqual(['a', 'b'])
    expect(rows.value).toEqual([])
  })

  it('turns every action into a menu row below md, the primary one first, and draws no button', () => {
    answerQueries(true)

    const { buttons, rows } = collapse(ACTIONS)

    expect(buttons.value).toEqual([])
    expect(rows.value.map((row) => row.testid)).toEqual(['b', 'a'])
  })

  it('knows whether an action is in the primary look, which makes the trigger a plus', () => {
    answerQueries(true)

    expect(collapse(ACTIONS).hasPrimary.value).toBe(true)
    expect(collapse([ACTIONS[0]!]).hasPrimary.value).toBe(false)
  })
})
