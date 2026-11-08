/**
 * Tests for runAction: success value, failure sentence from the API, the shared no-answer
 * sentence, error reset and the optional field-error capture.
 */

import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import { dropFieldError, runAction } from '@/lib/runAction'
import { ApiError } from '@/composables/useApi'

describe('runAction', () => {
  it('returns the value and leaves error null', async () => {
    /** The action error the helper writes to. */
    const error = ref<string | null>('old')
    const result = await runAction(error, async () => 7)

    expect(result).toEqual({ ok: true, value: 7 })
    expect(error.value).toBeNull()
  })

  it('stores the API detail sentence on failure', async () => {
    /** The action error the helper writes to. */
    const error = ref<string | null>(null)
    const result = await runAction(error, async () => {
      throw new ApiError(400, { type: 'X', detail: 'Server says no.' })
    })

    expect(result).toEqual({ ok: false })
    expect(error.value).toBe('Server says no.')
  })

  it('stores the shared no-answer sentence when nothing answered', async () => {
    /** The action error the helper writes to. */
    const error = ref<string | null>(null)
    await runAction(error, async () => {
      throw new Error('network')
    })

    expect(error.value).toBe('Could not reach the server. Check your connection and try again.')
  })

  it('captures the per-field messages and clears them on the next success', async () => {
    /** The action error the helper writes to. */
    const error = ref<string | null>(null)
    /** The field errors the helper writes to. */
    const fields = ref<Record<string, string>>({})

    await runAction(
      error,
      async () => {
        throw new ApiError(400, { type: 'VALIDATION_ERROR', detail: 'Invalid.', errors: { email: ['Required.'] } })
      },
      fields,
    )
    expect(fields.value).toEqual({ email: 'Required.' })

    await runAction(error, async () => 1, fields)
    expect(fields.value).toEqual({})
  })
})

describe('dropFieldError', () => {
  it('removes one field and keeps the others', () => {
    /** The field errors under test. */
    const errors = ref<Record<string, string>>({ sku: 'Taken.', metadata: 'Too large.' })

    dropFieldError(errors, 'sku')

    expect(errors.value).toEqual({ metadata: 'Too large.' })
  })

  it('replaces the map instead of mutating it, so watchers see the change', () => {
    const before = { sku: 'Taken.' }
    /** The field errors under test. */
    const errors = ref<Record<string, string>>(before)

    dropFieldError(errors, 'sku')

    expect(errors.value).not.toBe(before)
    expect(before).toEqual({ sku: 'Taken.' })
  })

  it('leaves the messages as they are for a field without one', () => {
    /** The field errors under test. */
    const errors = ref<Record<string, string>>({ sku: 'Taken.' })

    dropFieldError(errors, 'metadata')

    expect(errors.value).toEqual({ sku: 'Taken.' })
  })
})
