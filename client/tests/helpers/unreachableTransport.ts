/**
 * A transport whose backend does not answer at all, for the test of a read that gets no reply.
 *
 * @module tests/helpers/unreachableTransport
 */

import { startTransport } from '~/tests/helpers/stubTransport'

/**
 * Points `$fetch` at a stub that has already been closed, so every request is refused at the
 * connection like a backend or network that is down. It replaces the transport an earlier
 * `startTransport` call set up; that stub is still closed by its own test as usual.
 *
 * @returns Resolves once `$fetch` points at the closed address.
 */
export const makeBackendUnreachable = async (): Promise<void> => {
  const stub = await startTransport()
  await stub.close()
}
