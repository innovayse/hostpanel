/**
 * Unit tests for LogoLockup: the cube mark beside the site name when nothing is uploaded,
 * and the operator's wordmark winning once one is.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import LogoLockup from '~/components/LogoLockup.vue'
import { STATIC_LOGO_MARK } from '~/utils/appLogo'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('LogoLockup', () => {
  it('draws the bundled cube mark beside the operator site name when nothing is uploaded', async () => {
    await loadGlobals({ siteName: 'Hostpanel Demo' })
    const wrapper = mountWithKit(LogoLockup)

    expect(wrapper.get('[data-testid="app-logo-mark"]').attributes('src')).toContain(STATIC_LOGO_MARK)
    expect(wrapper.get('[data-testid="app-logo-name"]').text()).toBe('Hostpanel Demo')
    expect(wrapper.find('[data-testid="app-logo-light"]').exists()).toBe(false)
  })

  it('hides the mark from assistive technology, since the name beside it says the same', async () => {
    await loadGlobals()
    const mark = mountWithKit(LogoLockup).get('[data-testid="app-logo-mark"]')

    expect(mark.attributes('alt')).toBe('')
  })

  it('renders the uploaded wordmark instead of the mark and the name', async () => {
    await loadGlobals({ logo: '/api/uploads/logo.png' })
    const wrapper = mountWithKit(LogoLockup, { props: { imgClass: 'h-8' } })

    expect(wrapper.get('[data-testid="app-logo-light"]').attributes('src')).toBe('/api/uploads/logo.png')
    expect(wrapper.get('[data-testid="app-logo-light"]').classes()).toContain('h-8')
    expect(wrapper.find('[data-testid="app-logo-mark"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="app-logo-name"]').exists()).toBe(false)
  })
})
