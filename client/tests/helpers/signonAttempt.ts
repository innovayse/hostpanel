/**
 * Test helper: starts real sign-in attempts and hands back the cookies a browser would then send.
 *
 * It runs the real `startSignonFlow` (so the sealed transaction is the real one) and turns what the
 * response wrote into request cookies, so a test can play several attempts side by side, return
 * from each in any order, and keep what the browser still holds between steps.
 *
 * @module tests/helpers/signonAttempt
 */

import { makeEvent } from './bffHarness'
import { startSignonFlow } from '~/server/utils/signon-flow'
import { openTransaction } from '~/server/utils/signon-transaction'
import type { SignonTransaction } from '~/server/utils/signon-transaction'
import { SignonTransactionKind } from '~/types/signontransactionkind'

/** A started attempt. */
export interface StartedAttempt {
  /** The `state` the provider will return. */
  state: string
  /** The PKCE challenge sent to the provider. */
  challenge: string
  /** The browser's cookies after this start: the ones it held before plus this attempt's. */
  cookies: Record<string, string>
  /** The name of this attempt's transaction cookie. */
  cookieName: string
  /** The names the start deleted (pruned attempts). */
  deleted: string[]
}

/**
 * Starts an attempt in a browser that already holds some cookies.
 *
 * @param held - The cookies the browser holds before this start.
 * @param returnTo - The page to return to, already validated, or null.
 * @param kind - Interactive or silent.
 * @returns The attempt and the browser's cookies afterwards.
 */
export const startAttempt = (
  held: Record<string, string> = {},
  returnTo: string | null = null,
  kind: SignonTransactionKind = SignonTransactionKind.Interactive
): StartedAttempt => {
  const event = makeEvent({ cookies: held })
  const { params } = startSignonFlow(event, returnTo, kind)
  const cookies = { ...held }
  for (const name of event.deleted) {
    Reflect.deleteProperty(cookies, name)
  }
  for (const [name, written] of Object.entries(event.written)) {
    cookies[name] = written.value
  }
  const cookieName = Object.keys(event.written)[0] ?? ''
  return {
    state: params.get('state') ?? '',
    challenge: params.get('code_challenge') ?? '',
    cookies,
    cookieName,
    deleted: event.deleted,
  }
}

/**
 * Opens the transaction a route wrote to its response, the way the callback would later find it.
 *
 * @param event - The event the route handled.
 * @param state - The `state` the route put in the authorize address.
 * @returns The transaction, or null when none can be found for that state.
 */
export const writtenTransaction = (event: ReturnType<typeof makeEvent>, state: string | null): SignonTransaction | null => {
  const cookies: Record<string, string> = {}
  for (const [name, written] of Object.entries(event.written)) {
    cookies[name] = written.value
  }
  return openTransaction(makeEvent({ cookies }), state ?? undefined).transaction
}
