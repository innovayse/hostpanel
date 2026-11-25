/**
 * Unit tests for `constants/controlPanel.ts`.
 *
 * @module tests/constants/controlPanel.test
 */

import { describe, expect, it } from 'vitest'
import { WEBMAIL_PORT } from '~/constants/controlPanel'

describe('control panel constants', () => {
  it('give the webmail a valid TCP port', () => {
    expect(Number.isInteger(WEBMAIL_PORT)).toBe(true)
    expect(WEBMAIL_PORT).toBeGreaterThan(0)
    expect(WEBMAIL_PORT).toBeLessThan(65536)
  })
})
