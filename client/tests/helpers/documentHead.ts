/**
 * Reads what a component put on the document through a `useHead` spy.
 *
 * A component can call `useHead` several times (the document attributes, a structured-data getter), so a test
 * that reads "the first call" breaks the day another call is added before it. This finds the call by what it
 * carries instead: the object with `htmlAttrs`.
 *
 * @module tests/helpers/documentHead
 */

import type { Mock } from 'vitest'

/**
 * Finds the `useHead` call that sets attributes on `<html>`.
 *
 * @param useHead - The spy standing in for `useHead`.
 * @returns The object that call received, typed as the caller expects to read it.
 * @throws When no call carried `htmlAttrs`.
 */
export const documentHead = <T extends { htmlAttrs: unknown }>(useHead: Mock): T => {
  const input = useHead.mock.calls
    .map(([argument]) => argument)
    .find((argument) => typeof argument === 'object' && argument !== null && 'htmlAttrs' in argument)
  if (input === undefined) {
    throw new Error('useHead was never called with htmlAttrs.')
  }
  return input as T
}
