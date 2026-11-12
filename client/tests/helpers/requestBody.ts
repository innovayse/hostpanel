/**
 * Reads the JSON body of a request the real stub server received, so a page or store test asserts on
 * what actually crossed the wire instead of on a call to a function of ours.
 *
 * @module tests/helpers/requestBody
 */

import type { StubRequest } from './stubServer'

/**
 * The JSON body a request carried.
 *
 * @param request - The request the stub received; `undefined` when the test expected one that never arrived.
 * @returns The parsed body.
 * @throws When there is no request, or its body is not JSON: either way the expected request did not arrive as expected.
 */
export const bodyOf = (request: StubRequest | undefined): unknown => JSON.parse(request?.body ?? '')
