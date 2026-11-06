/**
 * Tests for `utils/headerWidget.ts`: which deployments are served the platform shell's widget.
 *
 * @module tests/utils/headerWidget.test
 */

import { describe, expect, it } from 'vitest'
import { isHeaderWidgetEnabled } from '~/utils/headerWidget'

describe('isHeaderWidgetEnabled', () => {
  it('is on for an sso deployment with a real portal address', () => {
    expect(isHeaderWidgetEnabled('sso', 'https://main.example.test')).toBe(true)
  })

  it('is off under local auth, which has no SSO for the widget to reach', () => {
    expect(isHeaderWidgetEnabled('local', 'https://main.example.test')).toBe(false)
  })

  it('is off when no portal address is configured', () => {
    expect(isHeaderWidgetEnabled('sso', '')).toBe(false)
    expect(isHeaderWidgetEnabled('sso', undefined)).toBe(false)
  })

  it('is off for the developer-machine default host', () => {
    expect(isHeaderWidgetEnabled('sso', 'http://portal.example.test:3000')).toBe(false)
  })

  it('is off when the auth mode is not set', () => {
    expect(isHeaderWidgetEnabled(undefined, 'https://main.example.test')).toBe(false)
  })
})
