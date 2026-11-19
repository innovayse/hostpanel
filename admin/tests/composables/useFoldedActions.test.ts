/**
 * Unit tests for useFoldedActions: every action is a button on a wide screen, and only the primary one on a phone.
 */
import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { useFoldedActions } from '@/composables/useFoldedActions'
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
 * @param matches - Whether the screen counts as a phone.
 */
const answerQueries = (matches: boolean): void => {
  window.matchMedia = ((query: string) => ({ matches, media: query, addEventListener: () => undefined, removeEventListener: () => undefined })) as unknown as typeof window.matchMedia
}

/**
 * Runs the composable inside a component, as a screen does, so its media query listener has an owner.
 *
 * @returns What the composable returned.
 */
const fold = (): ReturnType<typeof useFoldedActions<UiBarAction>> => {
  let result: ReturnType<typeof useFoldedActions<UiBarAction>> | undefined
  mount(defineComponent({
    /**
     * Calls the composable.
     *
     * @returns An empty render function.
     */
    setup: () => {
      result = useFoldedActions(() => ACTIONS, (action) => ({ label: action.label, icon: action.icon }))
      return () => null
    },
  }))
  return result as ReturnType<typeof useFoldedActions<UiBarAction>>
}

describe('useFoldedActions', () => {
  afterEach(() => {
    window.matchMedia = original
  })

  it('draws every action as a button and folds none on a wide screen', () => {
    answerQueries(false)

    const { buttons, folded } = fold()

    expect(buttons.value.map((action) => action.testid)).toEqual(['a', 'b'])
    expect(folded.value).toEqual([])
  })

  it('keeps the primary action and folds the others on a phone', () => {
    answerQueries(true)

    const { buttons, folded } = fold()

    expect(buttons.value.map((action) => action.testid)).toEqual(['b'])
    expect(folded.value.map((row) => row.label)).toEqual(['A'])
  })
})
