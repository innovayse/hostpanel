/**
 * Tests for `defineReadOnlyEventHandler`: a file route answers `GET` and `HEAD` with one handler and
 * refuses every other method with a coded 405 and an `Allow` header, before the handler runs.
 *
 * @module tests/server/utils/readOnlyEventHandler.test
 */

import { describe, expect, it, vi } from 'vitest'
import { makeEvent, refusal } from '../../helpers/bffHarness'
import { PortalErrorCode } from '~/constants/portalErrorCode'
import { defineReadOnlyEventHandler, isHeadRequest } from '~/server/utils/readOnlyEventHandler'
import { HttpMethod } from '~/types/httpmethod'

/** The two methods the wrapper lets through. */
const READ_METHODS = [HttpMethod.Get, HttpMethod.Head]

/** Every method the wrapper refuses. */
const REFUSED_METHODS = [HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Options]

describe('defineReadOnlyEventHandler', () => {
  it.each(READ_METHODS)('runs the handler once for %s and returns its answer', async (method) => {
    const handler = vi.fn(() => 'file')
    const route = defineReadOnlyEventHandler(handler)

    const answer = await route(makeEvent({ method }))

    expect(answer).toBe('file')
    expect(handler).toHaveBeenCalledTimes(1)
  })

  it.each(REFUSED_METHODS)('refuses %s with a coded 405 and an Allow header, without running the handler', async (method) => {
    const handler = vi.fn(() => 'file')
    const route = defineReadOnlyEventHandler(handler)
    const event = makeEvent({ method })

    const error = await refusal(() => route(event))

    expect(error).toMatchObject({ statusCode: 405, data: { code: PortalErrorCode.MethodNotAllowed } })
    expect(event.responseHeaders['Allow']).toBe('GET, HEAD')
    expect(handler).not.toHaveBeenCalled()
  })

  it('passes a refusal the handler throws on to the caller unchanged, for a HEAD as for a GET', async () => {
    const route = defineReadOnlyEventHandler(() => {
      throw Object.assign(new Error(''), { statusCode: 404 })
    })

    for (const method of READ_METHODS) {
      const error = await refusal(() => route(makeEvent({ method })))

      expect(error).toMatchObject({ statusCode: 404 })
    }
  })
})

describe('isHeadRequest', () => {
  it('is true for a HEAD and false for every other method', () => {
    expect(isHeadRequest(makeEvent({ method: HttpMethod.Head }))).toBe(true)
    for (const method of [HttpMethod.Get, ...REFUSED_METHODS]) {
      expect(isHeadRequest(makeEvent({ method }))).toBe(false)
    }
  })
})
