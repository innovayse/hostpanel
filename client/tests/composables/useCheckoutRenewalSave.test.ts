/**
 * Tests for `composables/useCheckoutRenewalSave.ts`: the stored-method session that follows a
 * confirmed checkout payment when the payer asked for it, spent once, and a non-blocking failure
 * when it cannot be opened.
 *
 * @module tests/composables/useCheckoutRenewalSave.test
 */

import { beforeEach, describe, expect, it, vi } from 'vitest'
import { rememberCheckoutSave } from '~/utils/pendingCheckoutSave'

/** The shared session starter a test makes answer. */
const start = vi.hoisted(() => vi.fn())

vi.mock('~/composables/useStoredMethodStart', () => ({
  /**
   * Stands in for the shared session starter.
   *
   * @returns The shared stand-in.
   */
  useStoredMethodStart: () => ({ start })
}))

/** The account the auth store stand-in reports; a test changes who is signed in. */
const auth = vi.hoisted(() => ({ user: null as { email: string } | null, fetchUser: vi.fn() }))

vi.mock('~/stores/auth', () => ({
  /**
   * Stands in for the auth store.
   *
   * @returns The shared stand-in.
   */
  useAuthStore: () => auth
}))

/** The account that made the checkout choice. */
const ADA = 'ada@example.com'

const { useCheckoutRenewalSave } = await import('~/composables/useCheckoutRenewalSave')

beforeEach(() => {
  start.mockReset()
  auth.user = { email: ADA }
  auth.fetchUser.mockReset().mockResolvedValue(undefined)
  window.sessionStorage.clear()
})

describe('useCheckoutRenewalSave', () => {
  it('starts nothing when the payer did not tick the box', async () => {
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(start).not.toHaveBeenCalled()
    expect(save.saving.value).toBe(false)
    expect(save.failed.value).toBe(false)
  })

  it('starts a session at the module the order was paid through, with the consent given at checkout', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    start.mockResolvedValue(true)
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(start).toHaveBeenCalledWith('card_gateway', true)
    // The browser is on its way to the gateway page: the page keeps showing progress, never a failure.
    expect(save.saving.value).toBe(true)
    expect(save.failed.value).toBe(false)
  })

  it('starts one session only, however often the paid page is opened', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    start.mockResolvedValue(true)
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)
    await save.startIfChosen(42)

    expect(start).toHaveBeenCalledTimes(1)
  })

  it('raises the notice, and stops showing progress, when the session cannot be opened', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    start.mockResolvedValue(false)
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(save.failed.value).toBe(true)
    expect(save.saving.value).toBe(false)
  })

  it('raises the notice instead of throwing when the start itself throws', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    start.mockRejectedValue(new Error('offline'))
    const save = useCheckoutRenewalSave()

    await expect(save.startIfChosen(42)).resolves.toBeUndefined()

    expect(save.failed.value).toBe(true)
    expect(save.saving.value).toBe(false)
  })

  it('drops the choice of a declined payment, so a later confirmation cannot act on it', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    const save = useCheckoutRenewalSave()

    save.discard(42)
    await save.startIfChosen(42)

    expect(start).not.toHaveBeenCalled()
  })

  it('starts nothing, and spends the choice, when another account returns to the page', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    auth.user = { email: 'bob@example.com' }
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(start).not.toHaveBeenCalled()
    expect(save.saving.value).toBe(false)
    expect(window.sessionStorage.length).toBe(0)
  })

  it('starts nothing when nobody can be identified', async () => {
    rememberCheckoutSave(42, 'card_gateway', ADA)
    auth.user = null
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(start).not.toHaveBeenCalled()
    expect(window.sessionStorage.length).toBe(0)
  })

  it('does not load the account when no choice was made', async () => {
    const save = useCheckoutRenewalSave()

    await save.startIfChosen(42)

    expect(auth.fetchUser).not.toHaveBeenCalled()
  })
})
