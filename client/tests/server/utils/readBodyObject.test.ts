/**
 * Tests for `server/utils/readBodyObject.ts`: a JSON object passes, anything else is a coded 400; named members must be strings.
 *
 * @module tests/server/utils/readBodyObject.test
 */

import { describe, expect, it } from 'vitest'
import { makeEvent, refusal } from '../../helpers/bffHarness'
import { BAD_BODY, readBodyObject, readBodyStrings } from '~/server/utils/readBodyObject'

describe('readBodyObject', () => {
  it('returns a JSON object as sent', async () => {
    expect(await readBodyObject(makeEvent({ body: { email: 'a@b.test' } }))).toEqual({ email: 'a@b.test' })
  })

  it.each([
    ['no body', undefined],
    ['null', null],
    ['a string', 'text'],
    ['a number', 7],
    ['an array', [1, 2]],
  ])('refuses %s with a coded 400 and no sentence', async (_name, body) => {
    const error = await refusal(() => readBodyObject(makeEvent({ body })))

    expect(error).toMatchObject({ statusCode: 400, data: { code: BAD_BODY } })
    expect(error['statusMessage']).toBeUndefined()
  })
})

describe('readBodyStrings', () => {
  it('returns only the named members when they are strings', async () => {
    expect(await readBodyStrings(makeEvent({ body: { a: 'x', b: 'y', c: 'z' } }), 'a', 'b')).toEqual({ a: 'x', b: 'y' })
  })

  it.each([
    ['a missing member', { a: 'x' }],
    ['a number', { a: 'x', b: 1 }],
    ['an object', { a: 'x', b: {} }],
    ['not a body object', 'text'],
  ])('refuses %s with a coded 400 and no sentence', async (_name, body) => {
    const error = await refusal(() => readBodyStrings(makeEvent({ body }), 'a', 'b'))

    expect(error).toMatchObject({ statusCode: 400, data: { code: BAD_BODY } })
    expect(error['statusMessage']).toBeUndefined()
  })
})
