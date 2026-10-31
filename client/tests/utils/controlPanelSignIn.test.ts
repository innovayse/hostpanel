/**
 * Unit tests for `utils/controlPanelSignIn.ts`.
 *
 * @module tests/utils/controlPanelSignIn.test
 */

import { describe, expect, it } from 'vitest'
import { canSignInToControlPanel } from '~/utils/controlPanelSignIn'
import { ServiceStatus } from '~/types/servicestatus'

/** A known server. */
const SERVER = { name: 'srv', hostname: 'srv.test', ipAddress: null }

describe('canSignInToControlPanel', () => {
  it('offers the sign-in for an active service on a known server to a person allowed single sign-on', () => {
    expect(canSignInToControlPanel({ status: ServiceStatus.Active, server: SERVER }, true)).toBe(true)
  })

  it('refuses a service that is not active', () => {
    expect(canSignInToControlPanel({ status: ServiceStatus.Pending, server: SERVER }, true)).toBe(false)
    expect(canSignInToControlPanel({ status: ServiceStatus.Suspended, server: SERVER }, true)).toBe(false)
  })

  it('refuses a service with no known server', () => {
    expect(canSignInToControlPanel({ status: ServiceStatus.Active, server: null }, true)).toBe(false)
  })

  it('refuses a person without single sign-on', () => {
    expect(canSignInToControlPanel({ status: ServiceStatus.Active, server: SERVER }, false)).toBe(false)
  })
})
