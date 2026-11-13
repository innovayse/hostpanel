/**
 * Stable client-side keys for rows of an editable list, so a row keeps its identity (and its input
 * focus) while rows are added or removed above it. A position is never a key.
 */

/** Last key handed out; module-wide, so two lists never share a key either. */
let lastRowKey = 0

/**
 * Hands out the next row key.
 *
 * @returns A number no earlier call returned.
 */
export const nextRowKey = (): number => {
  lastRowKey += 1
  return lastRowKey
}
