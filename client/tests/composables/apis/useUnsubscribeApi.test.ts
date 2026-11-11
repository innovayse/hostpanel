/**
 * Tests for `composables/apis/useUnsubscribeApi.ts`.
 *
 * The endpoint function is called for real through `apiFetch` against a stub server, and the test
 * asserts on the path, verb and body that arrived.
 *
 * @module tests/composables/apis/useUnsubscribeApi.test
 */

import { afterEach, describe, it } from 'vitest'
import { expectLastRequest, startTransport } from '../../helpers/stubTransport'
import type { StubServer } from '../../helpers/stubServer'
import { useUnsubscribeApi } from '~/composables/apis/useUnsubscribeApi'
import { HttpMethod } from '~/types/httpmethod'

/** The stub started by the current test. */
let stub: StubServer | undefined

afterEach(async () => {
  await stub?.close()
  stub = undefined
})

/** The api surface under test. */
const api = useUnsubscribeApi()

describe('useUnsubscribeApi', () => {
  it('unsubscribe posts the token to the BFF route', async () => {
    stub = await startTransport()

    await api.unsubscribe('tok')

    expectLastRequest(stub, {
      name: 'unsubscribe',
      call: () => api.unsubscribe('tok'),
      method: HttpMethod.Post,
      url: '/api/portal/public/unsubscribe',
      body: { token: 'tok' }
    })
  })
})
