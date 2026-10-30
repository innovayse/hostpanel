/**
 * Tests for `serviceAwaitsSetup`: which services the lists send to the setup wizard.
 *
 * @module tests/utils/serviceSetup.test
 */

import { describe, expect, it } from 'vitest'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'

describe('serviceAwaitsSetup', () => {
  it('is true for a hosting service that has no control-panel account yet', () => {
    expect(serviceAwaitsSetup({ isDigital: false, username: null })).toBe(true)
  })

  it('is false for a hosting service that is set up', () => {
    expect(serviceAwaitsSetup({ isDigital: false, username: 'webmstr' })).toBe(false)
  })

  it('is false for a digital service, which has nothing to set up and never carries a username', () => {
    expect(serviceAwaitsSetup({ isDigital: true, username: null })).toBe(false)
  })
})
