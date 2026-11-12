/**
 * Tests for `utils/externalRedirect`: only a secure absolute address (and an insecure one in
 * development) may be followed by the browser.
 *
 * @module tests/utils/externalRedirect.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { GatewaySessionKind } from '~/types/gatewaysessionkind'
import type { GatewayStart } from '~/types/gatewaystart'
import { isSafeGatewayStart, isSafeRedirectUrl, redirectToExternal, redirectToGateway } from '~/utils/externalRedirect'

describe('isSafeRedirectUrl', () => {
  it('accepts an absolute https address', () => {
    expect(isSafeRedirectUrl('https://bank.example/pay?id=1')).toBe(true)
  })

  it('refuses http outside development and accepts it in development', () => {
    expect(isSafeRedirectUrl('http://bank.example/pay')).toBe(false)
    expect(isSafeRedirectUrl('http://localhost:9000/pay', true)).toBe(true)
  })

  it('refuses script, data, relative, protocol-relative and malformed values in every mode', () => {
    for (const target of ['javascript:alert(1)', 'data:text/html,x', '/payment/result', '//bank.example/pay', 'not a url', '']) {
      expect(isSafeRedirectUrl(target, true)).toBe(false)
    }
  })
})

describe('redirectToExternal', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('hands the browser to a safe address', () => {
    const location = { href: '' }
    vi.stubGlobal('window', { location })

    expect(redirectToExternal('https://bank.example/pay')).toBe(true)
    expect(location.href).toBe('https://bank.example/pay')
  })

  it('does nothing for an unsafe address', () => {
    const location = { href: 'https://app.example/' }
    vi.stubGlobal('window', { location })

    expect(redirectToExternal('javascript:alert(1)')).toBe(false)
    expect(location.href).toBe('https://app.example/')
  })
})

describe('isSafeGatewayStart', () => {
  /**
   * A form post start whose form posts to an address.
   *
   * @param action - The address the form is posted to.
   * @returns The start.
   */
  const formPost = (action: string): GatewayStart => ({
    redirectUrl: action,
    kind: GatewaySessionKind.FormPost,
    formPost: { action, fields: [] }
  })

  it('accepts a redirect and a browser step by their page address', () => {
    for (const kind of [GatewaySessionKind.Redirect, GatewaySessionKind.BrowserStep]) {
      expect(isSafeGatewayStart({ redirectUrl: 'https://bank.example/pay', kind, formPost: null })).toBe(true)
      expect(isSafeGatewayStart({ redirectUrl: 'javascript:alert(1)', kind, formPost: null })).toBe(false)
    }
  })

  it('judges a form post by the action of its form', () => {
    expect(isSafeGatewayStart(formPost('https://bank.example/hosted'))).toBe(true)
    expect(isSafeGatewayStart(formPost('javascript:alert(1)'))).toBe(false)
    expect(isSafeGatewayStart({ redirectUrl: 'https://bank.example/hosted', kind: GatewaySessionKind.FormPost, formPost: null })).toBe(false)
  })
})

describe('redirectToGateway', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    document.body.replaceChildren()
  })

  it('opens the address of a redirect and of a browser step', () => {
    for (const kind of [GatewaySessionKind.Redirect, GatewaySessionKind.BrowserStep]) {
      const location = { href: '' }
      vi.stubGlobal('window', { location })

      expect(redirectToGateway({ redirectUrl: 'https://bank.example/step', kind, formPost: null })).toBe(true)
      expect(location.href).toBe('https://bank.example/step')
    }
  })

  it('submits a form post as a form the page builds, with every field as it came', () => {
    const submitted: HTMLFormElement[] = []
    vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(function (this: HTMLFormElement) {
      submitted.push(this)
    })
    const fields = [
      { name: 'token', value: 'abc' },
      { name: 'token', value: '"><script>alert(1)</script>' },
      { name: 'empty', value: '' }
    ]

    const handed = redirectToGateway({
      redirectUrl: 'https://bank.example/hosted',
      kind: GatewaySessionKind.FormPost,
      formPost: { action: 'https://bank.example/hosted', fields }
    })

    expect(handed).toBe(true)
    expect(submitted).toHaveLength(1)
    const form = submitted[0]!
    expect(form.method).toBe('post')
    expect(form.action).toBe('https://bank.example/hosted')
    const inputs = [...form.querySelectorAll('input')]
    expect(inputs.map(input => input.type)).toEqual(['hidden', 'hidden', 'hidden'])
    expect(inputs.map(input => ({ name: input.name, value: input.value }))).toEqual(fields)
    // Values are properties, never markup: nothing the plugin wrote became an element.
    expect(form.querySelector('script')).toBeNull()
    vi.restoreAllMocks()
  })

  it('does nothing for an answer the browser must not follow', () => {
    const location = { href: 'https://app.example/' }
    vi.stubGlobal('window', { location })
    const submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => undefined)

    expect(redirectToGateway({ redirectUrl: 'javascript:alert(1)', kind: GatewaySessionKind.Redirect, formPost: null })).toBe(false)
    expect(redirectToGateway({
      redirectUrl: 'http://x',
      kind: GatewaySessionKind.FormPost,
      formPost: { action: 'javascript:alert(1)', fields: [] }
    })).toBe(false)

    expect(location.href).toBe('https://app.example/')
    expect(submit).not.toHaveBeenCalled()
    vi.restoreAllMocks()
  })
})
