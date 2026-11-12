/**
 * Unit tests for NovaApps: a card per installer category and a count line that appears only when set.
 *
 * @module tests/templates/nova/sections/NovaApps.test
 */
import { computed } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import NovaApps from '~/templates/nova/sections/NovaApps.vue'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { balancedColumns } from '~/utils/balancedGrid'
import { stubTemplateGlobals } from '~/tests/helpers/templateGlobals'

/**
 * Stands in for `useInstallerApps`.
 *
 * @param count The app count line; empty hides it.
 */
const stubApps = (count: string): void => {
  vi.stubGlobal('useInstallerApps', () => computed(() => ({ count, categories: Object.values(InstallerAppCategory) })))
}

describe('NovaApps', () => {
  it('renders a card per category in the balanced grid', () => {
    stubTemplateGlobals()
    stubApps('150+')
    const wrapper = mountWithKit(NovaApps)
    const grid = wrapper.find('[data-testid="nova-apps-grid"]')

    expect(grid.findAll('li')).toHaveLength(Object.values(InstallerAppCategory).length)
    expect(grid.attributes('data-columns')).toBe(String(balancedColumns(6)))
  })

  it('shows the count line when the count is set', () => {
    stubTemplateGlobals()
    stubApps('150+')

    expect(mountWithKit(NovaApps).find('[data-testid="nova-apps-count"]').exists()).toBe(true)
  })

  it('hides the count line when the count is empty but keeps the categories', () => {
    stubTemplateGlobals()
    stubApps('')
    const wrapper = mountWithKit(NovaApps)

    expect(wrapper.find('[data-testid="nova-apps-count"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="nova-apps-grid"]').exists()).toBe(true)
  })
})
