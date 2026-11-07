/**
 * Unit tests for PluginVersionList: one row per version, the empty note, and the install event
 * passed up from a row.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginVersionList from '@/components/plugins/details/PluginVersionList.vue'
import type { RegistryPluginVersionDto } from '@/types/plugin'
import { createTestI18n } from '../../../helpers/i18n'
import { makeDetails } from '../../../helpers/pluginDetails'

/**
 * Mounts the list.
 *
 * @param versions - The versions to list; the fixture plugin's by default.
 * @returns The wrapper.
 */
const mountList = (versions: RegistryPluginVersionDto[] = makeDetails().versions) =>
  mount(PluginVersionList, {
    props: { versions, installedVersion: '2.0.0', stagedVersion: null, latestVersion: '3.0.0', busy: false },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginVersionList', () => {
  it('shows a row per version', () => {
    const wrapper = mountList()

    expect(wrapper.find('[data-testid="plugin-version-3.0.0"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-version-2.0.0"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-version-1.0.0"]').exists()).toBe(true)
  })

  it('shows the empty note when the registry lists no version', () => {
    const wrapper = mountList([])

    expect(wrapper.find('[data-testid="plugin-details-versions-empty"]').exists()).toBe(true)
  })

  it('passes a row install request up', async () => {
    const wrapper = mountList()

    await wrapper.get('[data-testid="plugin-version-install-3.0.0"]').trigger('click')

    expect(wrapper.emitted('install')).toEqual([['3.0.0']])
  })
})
