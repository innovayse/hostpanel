/**
 * Unit tests for `utils/controlPanel.ts`.
 *
 * @module tests/utils/controlPanel.test
 */

import { describe, expect, it } from 'vitest'
import { canSignInToControlPanel, controlPanelUrl, hasLiveHostingAccount, webmailUrl, websiteUrl } from '~/utils/controlPanel'
import { ServiceStatus } from '~/types/servicestatus'

describe('control panel addresses', () => {
  it('builds the secure panel sign-in address with the panel port', () => {
    expect(controlPanelUrl('srv.test')).toBe('https://srv.test:2083')
  })

  it('builds the secure webmail address with the webmail port', () => {
    expect(webmailUrl('srv.test')).toBe('https://srv.test:2096')
  })

  it('builds the secure website address without a port', () => {
    expect(websiteUrl('example.com')).toBe('https://example.com')
  })
})

/** A known server. */
const SERVER = { name: 'srv', hostname: 'srv.test', ipAddress: null }

describe('hasLiveHostingAccount', () => {
  it('is true for a pending, active or suspended service', () => {
    expect(hasLiveHostingAccount({ status: ServiceStatus.Pending })).toBe(true)
    expect(hasLiveHostingAccount({ status: ServiceStatus.Active })).toBe(true)
    expect(hasLiveHostingAccount({ status: ServiceStatus.Suspended })).toBe(true)
  })

  it('is false for a terminated service', () => {
    expect(hasLiveHostingAccount({ status: ServiceStatus.Terminated })).toBe(false)
  })
})

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
