/**
 * Tests for the outcome reporter: a success is a success toast, a refusal is a danger toast with the API's sentence,
 * and nothing else is kept on the screen.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'

describe('useOutcomeToast', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('toasts a success in the success tone', () => {
    useOutcomeToast().show('Rates updated', false)

    expect(useToastStore().toasts.map((toast) => [toast.message, toast.tone])).toEqual([['Rates updated', UiTone.Success]])
  })

  it('toasts a refusal in the danger tone', () => {
    useOutcomeToast().show('Could not reach the server', true)

    expect(useToastStore().toasts.map((toast) => [toast.message, toast.tone])).toEqual([
      ['Could not reach the server', UiTone.Danger],
    ])
  })

  it('keeps every outcome on screen in order', () => {
    const outcome = useOutcomeToast()

    outcome.show('failed', true)
    outcome.show('saved', false)

    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['failed', 'saved'])
  })
})
