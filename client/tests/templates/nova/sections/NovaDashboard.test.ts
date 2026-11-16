/**
 * Unit tests for NovaDashboard: the full section and the compact window-only form.
 */
import { beforeEach, describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import NovaDashboard from '~/templates/nova/sections/NovaDashboard.vue'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'

/** The stub server and the loader of the template globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useTemplateGlobals()

describe('NovaDashboard', () => {
  beforeEach(async () => {
    await loadGlobals()
  })

  it('renders the copy, the sign-in link and the window', () => {
    const wrapper = mountWithKit(NovaDashboard)

    expect(wrapper.find('h2').text()).toBe('nova.dashboard.title')
    expect(wrapper.find('[data-testid="nova-dashboard-cta"]').attributes('href')).toBe(CLIENT_LOGIN_PATH)
    expect(wrapper.find('[aria-hidden="true"]').exists()).toBe(true)
  })

  it('renders only the window when compact, with no heading', () => {
    const wrapper = mountWithKit(NovaDashboard, { props: { compact: true } })

    expect(wrapper.find('h2').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nova-dashboard-cta"]').exists()).toBe(false)
    expect(wrapper.findAll('li').length).toBeGreaterThan(0)
  })
})
