/**
 * Tests for `serviceAwaitsSetup`: the one rule that sends a service to the setup wizard, matching
 * the backend, which sets up only a Pending service.
 *
 * @module tests/utils/serviceSetup.test
 */

import { describe, expect, it } from 'vitest'
import { ServiceStatus } from '~/types/servicestatus'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'

describe('serviceAwaitsSetup', () => {
  it('is true for a pending hosting service', () => {
    expect(serviceAwaitsSetup({ isDigital: false, status: ServiceStatus.Pending })).toBe(true)
  })

  it.each([ServiceStatus.Active, ServiceStatus.Suspended, ServiceStatus.Terminated])('is false for a hosting service that is %s, whatever its username', (status) => {
    expect(serviceAwaitsSetup({ isDigital: false, status })).toBe(false)
  })

  it('is false for a pending digital service, which has nothing to set up', () => {
    expect(serviceAwaitsSetup({ isDigital: true, status: ServiceStatus.Pending })).toBe(false)
  })
})
