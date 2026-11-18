/**
 * Unit tests for ClientAuthLogo: the home link around the real logo, with the operator's brand settings read through
 * the real composables from the settings stub server.
 *
 * @module tests/components/client/AuthLogo.test
 */

import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import AuthLogo from '~/components/client/AuthLogo.vue'

/** The stub server and the loader of the operator's settings; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('ClientAuthLogo', () => {
  it('links the bundled wordmark to the home page when nothing is uploaded', async () => {
    await loadGlobals()
    const wrapper = mountClient(AuthLogo)

    const link = wrapper.get('[data-testid="auth-logo"]')
    expect(link.attributes('href')).toBe('/')
    expect(link.get('[data-testid="app-logo-light"]').attributes('src')).toContain('/logo.svg')
    expect(link.get('[data-testid="app-logo-dark"]').attributes('src')).toContain('/logo-dark.svg')
  })

  it('links the operator upload to the home page once one is set', async () => {
    await loadGlobals({ logo: '/api/uploads/logo.png' })
    const wrapper = mountClient(AuthLogo)

    const link = wrapper.get('[data-testid="auth-logo"]')
    expect(link.attributes('href')).toBe('/')
    expect(link.get('[data-testid="app-logo-light"]').attributes('src')).toBe('/api/uploads/logo.png')
  })
})
