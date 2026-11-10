/**
 * Unit tests for PluginVersionRow: number, date and status badges, host compatibility, release
 * notes, permissions, the reason and replacement of a withdrawn version, and when the install
 * button is offered, labelled and blocked.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginVersionRow from '@/components/plugins/details/PluginVersionRow.vue'
import { PluginVersionStatus } from '@/types/plugin'
import type { RegistryPluginVersionDto } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeVersion } from '../../../helpers/pluginDetails'

/**
 * Mounts a row.
 *
 * @param version - Fields of the version to change.
 * @param props - Other props to change.
 * @returns The wrapper.
 */
const mountRow = (version: Partial<RegistryPluginVersionDto> = {}, props: Record<string, unknown> = {}) =>
  mount(PluginVersionRow, {
    props: {
      version: makeVersion({ version: '3.0.0', ...version }),
      installedVersion: '2.0.0',
      stagedVersion: null,
      latestVersion: '3.0.0',
      busy: false,
      ...props,
    },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginVersionRow', () => {
  it('shows the number, status, newest mark and compatibility', () => {
    const wrapper = mountRow()

    expect(wrapper.text()).toContain('3.0.0')
    expect(wrapper.get('[data-testid="plugin-version-status-3.0.0"]').text()).toBe('Published')
    expect(wrapper.find('[data-testid="plugin-version-latest-3.0.0"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-version-compatibility-3.0.0"]').text()).toBe('Runs on this host')
    expect(wrapper.get('[data-testid="plugin-version-date-3.0.0"]').text()).toContain('Published 15/01/2026')
  })

  it('says the host range of a bounded version and marks an incompatible one', () => {
    const wrapper = mountRow({ minHostVersion: '1.0.0', maxHostVersion: '1.9.0', compatible: false })

    expect(wrapper.get('[data-testid="plugin-version-date-3.0.0"]').text()).toContain('Host versions 1.0.0 to 1.9.0')
    expect(wrapper.get('[data-testid="plugin-version-compatibility-3.0.0"]').text()).toBe('Does not run on this host')
  })

  it('shows the release notes and the permissions, or says there are none', () => {
    const withBoth = mountRow({ changelog: 'Faster refunds.', permissions: ['payments.charge', 'orders.read'] })
    const withNone = mountRow()

    expect(withBoth.get('[data-testid="plugin-version-changelog-3.0.0"]').text()).toBe('Faster refunds.')
    expect(withBoth.get('[data-testid="plugin-version-permissions-3.0.0"]').text()).toContain('payments.charge')
    expect(withBoth.get('[data-testid="plugin-version-permissions-3.0.0"]').text()).toContain('orders.read')
    expect(withNone.get('[data-testid="plugin-version-changelog-3.0.0"]').text()).toBe('No release notes.')
    expect(withNone.find('[data-testid="plugin-version-no-permissions-3.0.0"]').exists()).toBe(true)
  })

  it('shows the reason and the replacement of a yanked version and blocks its install', () => {
    const wrapper = mountRow({ status: PluginVersionStatus.Yanked, statusReason: 'Charges twice.', replacementVersion: '2.5.0' })

    expect(wrapper.get('[data-testid="plugin-version-reason-3.0.0"]').text()).toContain('Charges twice.')
    expect(wrapper.get('[data-testid="plugin-version-replacement-3.0.0"]').text()).toContain('2.5.0')
    expect(wrapper.get('[data-testid="plugin-version-install-3.0.0"]').attributes('disabled')).toBeDefined()
  })

  it('warns about a deprecated version but still allows its install', () => {
    const wrapper = mountRow({ status: PluginVersionStatus.Deprecated })

    expect(wrapper.get('[data-testid="plugin-version-status-3.0.0"]').text()).toBe('Deprecated')
    expect(wrapper.find('[data-testid="plugin-version-notice-3.0.0"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-version-install-3.0.0"]').attributes('disabled')).toBeUndefined()
  })

  it('labels the button Update for a newer version and emits the version', async () => {
    const wrapper = mountRow()

    expect(wrapper.get('[data-testid="plugin-version-install-3.0.0"]').text()).toBe('Update')
    await wrapper.get('[data-testid="plugin-version-install-3.0.0"]').trigger('click')

    expect(wrapper.emitted('install')).toEqual([['3.0.0']])
  })

  it('labels the button Downgrade for an older version', () => {
    const wrapper = mountRow({ version: '1.0.0' })

    expect(wrapper.get('[data-testid="plugin-version-install-1.0.0"]').text()).toBe('Downgrade')
  })

  it('labels the button Install when nothing is installed', () => {
    const wrapper = mountRow({}, { installedVersion: null })

    expect(wrapper.get('[data-testid="plugin-version-install-3.0.0"]').text()).toBe('Install')
  })

  it('marks the installed version and offers no button for it', () => {
    const wrapper = mountRow({ version: '2.0.0' })

    expect(wrapper.find('[data-testid="plugin-version-installed-2.0.0"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-version-install-2.0.0"]').exists()).toBe(false)
  })

  it('offers no install button for the version already staged', () => {
    const wrapper = mountRow({}, { stagedVersion: '3.0.0' })

    expect(wrapper.find('[data-testid="plugin-version-install-3.0.0"]').exists()).toBe(false)
  })

  it('marks the staged version', () => {
    const wrapper = mountRow({}, { stagedVersion: '3.0.0' })

    expect(wrapper.find('[data-testid="plugin-version-staged-3.0.0"]').exists()).toBe(true)
  })

  it('blocks the install of a version that does not run on this host, and while busy', () => {
    const incompatible = mountRow({ compatible: false })
    const busy = mountRow({}, { busy: true })

    expect(incompatible.get('[data-testid="plugin-version-install-3.0.0"]').attributes('disabled')).toBeDefined()
    expect(busy.get('[data-testid="plugin-version-install-3.0.0"]').attributes('disabled')).toBeDefined()
  })
})
