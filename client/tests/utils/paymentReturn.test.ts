/**
 * Tests for `utils/paymentReturn`: the parameters a gateway's return request carried are read from the route
 * query in the order they arrived, without the result page's own `order` and `invoice`.
 *
 * @module tests/utils/paymentReturn.test
 */

import { describe, expect, it } from 'vitest'
import { returnParametersOf } from '~/utils/paymentReturn'

describe('returnParametersOf', () => {
  it('lists every name and value in the order they arrived', () => {
    expect(returnParametersOf({ nonce: 'n1', token: 't1' })).toEqual([
      { name: 'nonce', value: 'n1' },
      { name: 'token', value: 't1' }
    ])
  })

  it('keeps every value of a repeated name, in order, for the backend to choose from', () => {
    expect(returnParametersOf({ token: ['a', 'b'] })).toEqual([
      { name: 'token', value: 'a' },
      { name: 'token', value: 'b' }
    ])
  })

  it('reads a name without a value as an empty value', () => {
    expect(returnParametersOf({ flag: null, other: [null, 'x'] })).toEqual([
      { name: 'flag', value: '' },
      { name: 'other', value: '' },
      { name: 'other', value: 'x' }
    ])
  })

  it('leaves out the parameters the result page reads itself', () => {
    expect(returnParametersOf({ order: '5', invoice: '9', nonce: 'n1' })).toEqual([{ name: 'nonce', value: 'n1' }])
  })

  it('is empty when the return carried nothing of the gateway', () => {
    expect(returnParametersOf({})).toEqual([])
    expect(returnParametersOf({ order: '5' })).toEqual([])
  })
})
