/**
 * Tests for `serviceAwaitsSetup` and `serviceSetupPending`: which services the lists and the detail
 * page send to the setup wizard.
 *
 * @module tests/utils/serviceSetup.test
 */

import { describe, expect, it } from 'vitest'
import { ServiceStatus } from '~/types/servicestatus'
import { serviceAwaitsSetup, serviceSetupPending } from '~/utils/serviceSetup'

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

describe('serviceSetupPending', () => {
  it('is true for a pending hosting service', () => {
    expect(serviceSetupPending({ isDigital: false, status: ServiceStatus.Pending })).toBe(true)
  })

  it('is false for a hosting service that is not pending', () => {
    expect(serviceSetupPending({ isDigital: false, status: ServiceStatus.Active })).toBe(false)
  })

  it('is false for a pending digital service, which has nothing to set up', () => {
    expect(serviceSetupPending({ isDigital: true, status: ServiceStatus.Pending })).toBe(false)
  })
})
