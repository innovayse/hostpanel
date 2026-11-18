/**
 * Unit tests for AppLauncher: invisible without apps, a tile per app with a token gradient and no
 * inline style, and closing on a choice.
 *
 * The real app-launcher composable reads the operator's settings from the stub server.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import AppLauncher from '~/templates/aurora/layout/AppLauncher.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SETTING_ON } from '~/constants/settingValue'
import { useAuroraGlobals } from '../auroraGlobals'

/** The stub server and the loader of the aurora globals; the hooks start the stub before each test and close it after. */
const { loadGlobals } = useAuroraGlobals()

/**
 * The settings of an operator who switched the launcher on and gave these apps an address.
 *
 * @param apps - The setting key of each app and the address the operator entered for it.
 * @returns The settings by their dotted key.
 */
const launcherSettings = (apps: Array<[PortalSettingKey, string]>): Record<string, string> =>
  Object.fromEntries([[PortalSettingKey.AppsEnabled, SETTING_ON], ...apps])

describe('AppLauncher', () => {
  it('renders nothing for a deployment with no apps', async () => {
    await loadGlobals({ settings: launcherSettings([]) })
    const wrapper = mountWithKit(AppLauncher)

    expect(wrapper.find('[data-testid="app-launcher-trigger"]').exists()).toBe(false)
  })

  it('renders nothing while the operator has not switched the launcher on', async () => {
    await loadGlobals({ settings: { [PortalSettingKey.AppsTasks]: 'https://tasks.test' } })
    const wrapper = mountWithKit(AppLauncher)

    expect(wrapper.find('[data-testid="app-launcher-trigger"]').exists()).toBe(false)
  })

  it('opens a panel with one link per app', async () => {
    await loadGlobals({ settings: launcherSettings([[PortalSettingKey.AppsTasks, 'https://tasks.test'], [PortalSettingKey.AppsErp, 'https://erp.test']]) })
    const wrapper = mountWithKit(AppLauncher)

    await wrapper.find('[data-testid="app-launcher-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="app-launcher-tasks"]').attributes('href')).toBe('https://tasks.test')
    expect(wrapper.find('[data-testid="app-launcher-erp"]').text()).toContain('aurora.apps.items.erp.label')
    expect(wrapper.find('[data-testid="app-launcher-erp"]').text()).toContain('aurora.apps.items.erp.desc')
  })

  it('paints each tile from a gradient token keyed by app id, with no inline style', async () => {
    await loadGlobals({ settings: launcherSettings([[PortalSettingKey.AppsTasks, 'https://tasks.test'], [PortalSettingKey.AppsCalendar, 'https://calendar.test']]) })
    const wrapper = mountWithKit(AppLauncher)
    await wrapper.find('[data-testid="app-launcher-trigger"]').trigger('click')

    expect(wrapper.find('[data-testid="app-launcher-tasks"] .place-items-center').classes()).toContain('bg-tile-mint')
    expect(wrapper.find('[data-testid="app-launcher-calendar"] .place-items-center').classes()).toContain('bg-tile-rose')
    expect(wrapper.html()).not.toContain(' style=')
  })

  it('closes the panel when an app is chosen', async () => {
    await loadGlobals({ settings: launcherSettings([[PortalSettingKey.AppsTasks, 'https://tasks.test']]) })
    const wrapper = mountWithKit(AppLauncher)
    await wrapper.find('[data-testid="app-launcher-trigger"]').trigger('click')

    await wrapper.find('[data-testid="app-launcher-tasks"]').trigger('click')

    expect(wrapper.find('[data-testid="ui-popover-panel"]').exists()).toBe(false)
  })
})
